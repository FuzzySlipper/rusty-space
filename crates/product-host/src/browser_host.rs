//! browser-host: a minimal HTTP + WebSocket delivery adapter for the live
//! Space product service. It serves the built web shell, observes wall-clock
//! time, translates typed input, and delivers renderer-neutral product updates.

use std::io::{ErrorKind, Read, Write};
use std::net::{SocketAddr, TcpListener, TcpStream};
use std::path::{Component, Path, PathBuf};
use std::sync::{Arc, Mutex};
use std::time::{Duration, Instant};

use rusty_space_runtime::{
    SpaceProductCommand, SpaceProductCommandReceipt, SpaceProductService, SpaceProductSession,
    SpaceProductSessionBaseline, SpaceProductSessionError, SpaceProductUpdate,
};
use serde::{Deserialize, Serialize};
use tungstenite::{Message, accept};

const DEFAULT_ADDRESS: &str = "127.0.0.1:8787";
const SESSION_PATH: &str = "/api/session";

type SharedService = Arc<Mutex<SpaceProductService>>;

/// Guarantees that every completed or unwound transport turn releases its
/// controller lease. A stale guard is harmless because the Rust service fences
/// release by generation.
struct SessionReleaseGuard {
    service: SharedService,
    session: SpaceProductSession,
}

impl Drop for SessionReleaseGuard {
    fn drop(&mut self) {
        self.service
            .lock()
            .expect("live service lock")
            .release_session(self.session);
    }
}

#[derive(Debug, Deserialize)]
#[serde(tag = "type", rename_all = "camelCase", deny_unknown_fields)]
enum BrowserCommand {
    SetFlightIntent {
        generation: u64,
        throttle: f64,
        turn: f64,
    },
    ResetFlight {
        generation: u64,
    },
}

impl BrowserCommand {
    const fn generation(&self) -> u64 {
        match self {
            Self::SetFlightIntent { generation, .. } | Self::ResetFlight { generation } => {
                *generation
            }
        }
    }

    const fn into_product_command(self) -> SpaceProductCommand {
        match self {
            Self::SetFlightIntent { throttle, turn, .. } => {
                SpaceProductCommand::SetFlightIntent { throttle, turn }
            }
            Self::ResetFlight { .. } => SpaceProductCommand::ResetFlight,
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
enum CommandRejectionCode {
    MalformedCommand,
    UnsupportedCommand,
    StaleGeneration,
    InvalidCommand,
}

#[derive(Debug, Serialize)]
#[serde(tag = "type", rename_all = "camelCase")]
enum ServerMessage {
    /// A full `Create` frame required before retained-frame diffs are valid.
    Baseline {
        generation: u64,
        update: SpaceProductUpdate,
    },
    Update {
        generation: u64,
        update: SpaceProductUpdate,
    },
    CommandReceipt {
        generation: u64,
        receipt: SpaceProductCommandReceipt,
    },
    CommandRejected {
        generation: u64,
        code: CommandRejectionCode,
        message: String,
    },
}

#[derive(Debug)]
struct CommandRejection {
    code: CommandRejectionCode,
    message: String,
}

#[derive(Debug, Clone, PartialEq, Eq)]
struct HostArguments {
    address: SocketAddr,
    dist: PathBuf,
    handling: PathBuf,
}

fn main() {
    let arguments = arguments().unwrap_or_else(|error| panic!("{error}"));
    let dist = arguments.dist.canonicalize().unwrap_or_else(|error| {
        panic!(
            "browser shell dist {} is unavailable: {error}",
            arguments.dist.display()
        )
    });
    assert!(
        dist.join("index.html").is_file(),
        "browser shell is not built"
    );

    let service =
        SpaceProductService::admit(&std::fs::read(&arguments.handling).unwrap_or_else(|error| {
            panic!(
                "cannot read ship handling package {}: {error}",
                arguments.handling.display()
            )
        }))
        .unwrap_or_else(|error| panic!("live service admission failed: {error}"));
    let service = Arc::new(Mutex::new(service));
    start_driver(&service);

    let listener = TcpListener::bind(arguments.address).unwrap_or_else(|error| {
        panic!("cannot bind browser host at {}: {error}", arguments.address)
    });
    let address = listener
        .local_addr()
        .expect("bound listener has an address");
    println!("browser-host listening at http://{address}");

    for stream in listener.incoming() {
        match stream {
            Ok(stream) => {
                let service = Arc::clone(&service);
                let dist = dist.clone();
                std::thread::spawn(move || handle_connection(stream, &service, &dist));
            }
            Err(error) => eprintln!("browser-host accept error: {error}"),
        }
    }
}

/// Observe wall-clock time and hand it to the service. The service, rather
/// than this transport adapter, owns its fixed-step accumulation policy.
fn start_driver(service: &SharedService) {
    let service = Arc::clone(service);
    std::thread::spawn(move || {
        let mut previous = Instant::now();
        loop {
            std::thread::sleep(Duration::from_millis(1));
            let now = Instant::now();
            let elapsed = now.saturating_duration_since(previous);
            previous = now;
            if let Err(error) = service
                .lock()
                .expect("live service lock")
                .advance_elapsed(elapsed)
            {
                eprintln!("browser-host product advance error: {error}");
            }
        }
    });
}

fn arguments() -> Result<HostArguments, String> {
    parse_arguments(std::env::args().skip(1))
}

fn parse_arguments(
    arguments: impl IntoIterator<Item = impl Into<String>>,
) -> Result<HostArguments, String> {
    let mut address = DEFAULT_ADDRESS
        .parse::<SocketAddr>()
        .expect("default browser-host address");
    let mut dist = Path::new(env!("CARGO_MANIFEST_DIR")).join("../../apps/web/dist");
    let mut handling = Path::new(env!("CARGO_MANIFEST_DIR"))
        .join("../../content/gameplay/rusty-space-core.package.json");
    let mut args = arguments.into_iter().map(Into::into);
    while let Some(argument) = args.next() {
        match argument.as_str() {
            "--addr" => {
                address = args
                    .next()
                    .ok_or_else(|| "--addr needs a value".to_owned())?
                    .parse()
                    .map_err(|error| format!("--addr must be a socket address: {error}"))?;
            }
            "--dist" => {
                dist = PathBuf::from(
                    args.next()
                        .ok_or_else(|| "--dist needs a value".to_owned())?,
                );
            }
            "--handling" => {
                handling = PathBuf::from(
                    args.next()
                        .ok_or_else(|| "--handling needs a value".to_owned())?,
                );
            }
            _ => return Err(format!("unknown browser-host argument {argument}")),
        }
    }
    Ok(HostArguments {
        address,
        dist,
        handling,
    })
}

fn handle_connection(mut stream: TcpStream, service: &SharedService, dist: &Path) {
    if session_upgrade_requested(&stream) {
        run_session(stream, Arc::clone(service));
        return;
    }
    let request = match read_request(&mut stream) {
        Ok(request) => request,
        Err(message) => {
            let _ = write_response(
                &mut stream,
                400,
                "text/plain; charset=utf-8",
                message.into(),
            );
            return;
        }
    };
    let path = request.path.split('?').next().unwrap_or(&request.path);
    let response = route(request.method.as_str(), path, dist);
    let _ = write_response(&mut stream, response.0, response.1, response.2);
}

fn session_upgrade_requested(stream: &TcpStream) -> bool {
    let mut prefix = [0_u8; 512];
    stream
        .peek(&mut prefix)
        .ok()
        .and_then(|length| std::str::from_utf8(&prefix[..length]).ok())
        .is_some_and(|request| request.starts_with(&format!("GET {SESSION_PATH} ")))
}

fn run_session(stream: TcpStream, service: SharedService) {
    let mut websocket = match accept(stream) {
        Ok(websocket) => websocket,
        Err(error) => {
            eprintln!("browser-host WebSocket handshake failed: {error}");
            return;
        }
    };
    let _ = websocket
        .get_ref()
        .set_read_timeout(Some(Duration::from_millis(1)));

    let baseline = match service.lock().expect("live service lock").open_session() {
        Ok(baseline) => baseline,
        Err(error) => {
            eprintln!("browser-host cannot open session: {error}");
            let _ = websocket.close(None);
            return;
        }
    };
    let session = baseline.session;
    let _release = SessionReleaseGuard {
        service: Arc::clone(&service),
        session,
    };
    let result = run_session_loop(&mut websocket, &service, session, baseline);
    if let Err(error) = result {
        eprintln!("browser-host session transport failed: {error}");
    }
}

fn run_session_loop(
    websocket: &mut tungstenite::WebSocket<TcpStream>,
    service: &SharedService,
    session: SpaceProductSession,
    baseline: SpaceProductSessionBaseline,
) -> Result<(), tungstenite::Error> {
    send_server_message(
        websocket,
        ServerMessage::Baseline {
            generation: session.generation,
            update: baseline.update.clone(),
        },
    )?;

    let mut published_sequence = Some(baseline.update.sequence);
    loop {
        // Drain up to a bounded batch of incoming input intents.
        let mut drained = 0;
        while drained < 32 {
            match websocket.read() {
                Ok(Message::Text(text)) => match parse_browser_command(&text) {
                    Ok(command) if command.generation() != session.generation => {
                        send_rejection(
                            websocket,
                            session,
                            CommandRejection {
                                code: CommandRejectionCode::StaleGeneration,
                                message: format!(
                                    "command generation {} does not match this session",
                                    command.generation()
                                ),
                            },
                        )?;
                    }
                    Ok(command) => {
                        let result = service
                            .lock()
                            .expect("live service lock")
                            .submit_session_command(session, command.into_product_command());
                        match result {
                            Ok(receipt) => send_server_message(
                                websocket,
                                ServerMessage::CommandReceipt {
                                    generation: session.generation,
                                    receipt,
                                },
                            )?,
                            Err(error) => send_rejection(
                                websocket,
                                session,
                                command_rejection_from_service(error),
                            )?,
                        }
                    }
                    Err(rejection) => send_rejection(websocket, session, rejection)?,
                },
                Ok(Message::Close(frame)) => {
                    let _ = websocket.close(frame);
                    return Ok(());
                }
                Ok(Message::Ping(payload)) => {
                    websocket.send(Message::Pong(payload))?;
                }
                Ok(Message::Pong(_)) | Ok(Message::Frame(_)) => {}
                Ok(Message::Binary(_)) => send_rejection(
                    websocket,
                    session,
                    CommandRejection {
                        code: CommandRejectionCode::UnsupportedCommand,
                        message: "binary commands are unsupported".to_owned(),
                    },
                )?,
                Err(error) if websocket_would_block(&error) => break,
                Err(_) => return Ok(()),
            }
            drained += 1;
        }

        // Push the latest frame when it has advanced.
        let update = service
            .lock()
            .expect("live service lock")
            .latest_update()
            .clone();
        if published_sequence != Some(update.sequence) {
            let update_sequence = update.sequence;
            send_server_message(
                websocket,
                ServerMessage::Update {
                    generation: session.generation,
                    update,
                },
            )?;
            published_sequence = Some(update_sequence);
        }
    }
}

fn parse_browser_command(text: &str) -> Result<BrowserCommand, CommandRejection> {
    let value: serde_json::Value =
        serde_json::from_str(text).map_err(|error| CommandRejection {
            code: CommandRejectionCode::MalformedCommand,
            message: format!("command is not valid JSON: {error}"),
        })?;
    let command_type = value
        .get("type")
        .and_then(serde_json::Value::as_str)
        .map(str::to_owned)
        .ok_or_else(|| CommandRejection {
            code: CommandRejectionCode::MalformedCommand,
            message: "command type is missing".to_owned(),
        })?;
    if command_type != "setFlightIntent" && command_type != "resetFlight" {
        return Err(CommandRejection {
            code: CommandRejectionCode::UnsupportedCommand,
            message: format!("unsupported command type {command_type}"),
        });
    }
    serde_json::from_value(value).map_err(|error| CommandRejection {
        code: CommandRejectionCode::MalformedCommand,
        message: format!("{command_type} command is malformed: {error}"),
    })
}

fn command_rejection_from_service(error: SpaceProductSessionError) -> CommandRejection {
    let code = match error {
        SpaceProductSessionError::StaleSession(_) => CommandRejectionCode::StaleGeneration,
        SpaceProductSessionError::InvalidCommand(_) => CommandRejectionCode::InvalidCommand,
    };
    CommandRejection {
        code,
        message: error.to_string(),
    }
}

fn send_rejection(
    websocket: &mut tungstenite::WebSocket<TcpStream>,
    session: SpaceProductSession,
    rejection: CommandRejection,
) -> Result<(), tungstenite::Error> {
    send_server_message(
        websocket,
        ServerMessage::CommandRejected {
            generation: session.generation,
            code: rejection.code,
            message: rejection.message,
        },
    )
}

fn send_server_message(
    websocket: &mut tungstenite::WebSocket<TcpStream>,
    message: ServerMessage,
) -> Result<(), tungstenite::Error> {
    let encoded = serde_json::to_string(&message).expect("encode server message");
    websocket.send(Message::Text(encoded.into()))
}

fn websocket_would_block(error: &tungstenite::Error) -> bool {
    matches!(
        error,
        tungstenite::Error::Io(io_error)
            if matches!(io_error.kind(), ErrorKind::WouldBlock | ErrorKind::TimedOut)
    )
}

struct HttpRequest {
    method: String,
    path: String,
}

fn read_request(stream: &mut TcpStream) -> Result<HttpRequest, String> {
    let mut request = Vec::new();
    let mut buffer = [0_u8; 2_048];
    let header_end = loop {
        let read = stream
            .read(&mut buffer)
            .map_err(|error| error.to_string())?;
        if read == 0 {
            return Err("request ended before its headers".to_owned());
        }
        request.extend_from_slice(&buffer[..read]);
        if let Some(index) = request.windows(4).position(|window| window == b"\r\n\r\n") {
            break index + 4;
        }
        if request.len() > 16_384 {
            return Err("request headers are too large".to_owned());
        }
    };
    let head = String::from_utf8(request[..header_end].to_vec())
        .map_err(|_| "request headers are not UTF-8".to_owned())?;
    let mut parts = head.lines().next().unwrap_or_default().split_whitespace();
    let method = parts.next().ok_or("request method is missing")?.to_owned();
    let path = parts.next().ok_or("request path is missing")?.to_owned();
    Ok(HttpRequest { method, path })
}

fn route(method: &str, path: &str, dist: &Path) -> (u16, &'static str, Vec<u8>) {
    match (method, path) {
        ("GET", "/health") => (
            200,
            "application/json; charset=utf-8",
            serde_json::to_vec(&serde_json::json!({ "project": "rusty-space", "status": "ok" }))
                .expect("encode health response"),
        ),
        ("GET", _) | ("HEAD", _) => serve_static(method, path, dist),
        _ => error_response(405, "method not allowed"),
    }
}

fn serve_static(method: &str, path: &str, dist: &Path) -> (u16, &'static str, Vec<u8>) {
    let relative = if path == "/" {
        PathBuf::from("index.html")
    } else {
        PathBuf::from(path.trim_start_matches('/'))
    };
    if relative.components().any(|component| {
        matches!(
            component,
            Component::ParentDir | Component::RootDir | Component::Prefix(_)
        )
    }) {
        return (403, "text/plain; charset=utf-8", b"forbidden\n".to_vec());
    }
    let file = dist.join(&relative);
    if !file.is_file() {
        return (404, "text/plain; charset=utf-8", b"not found\n".to_vec());
    }
    let content_type = match file.extension().and_then(|extension| extension.to_str()) {
        Some("html") => "text/html; charset=utf-8",
        Some("js") => "text/javascript; charset=utf-8",
        Some("css") => "text/css; charset=utf-8",
        Some("json") => "application/json; charset=utf-8",
        Some("svg") => "image/svg+xml",
        _ => "application/octet-stream",
    };
    let body = if method == "HEAD" {
        Vec::new()
    } else {
        match std::fs::read(&file) {
            Ok(body) => body,
            Err(_) => return (500, "text/plain; charset=utf-8", b"read error\n".to_vec()),
        }
    };
    (200, content_type, body)
}

fn error_response(status: u16, message: &str) -> (u16, &'static str, Vec<u8>) {
    (
        status,
        "application/json; charset=utf-8",
        serde_json::to_vec(&serde_json::json!({ "error": message })).expect("encode error"),
    )
}

fn write_response(
    stream: &mut TcpStream,
    status: u16,
    content_type: &str,
    body: Vec<u8>,
) -> std::io::Result<()> {
    let reason = match status {
        200 => "OK",
        400 => "Bad Request",
        403 => "Forbidden",
        404 => "Not Found",
        405 => "Method Not Allowed",
        _ => "Internal Server Error",
    };
    write!(
        stream,
        "HTTP/1.1 {status} {reason}\r\nContent-Type: {content_type}\r\nContent-Length: {}\r\nCache-Control: no-store\r\nContent-Security-Policy: default-src 'self'; connect-src 'self' ws://127.0.0.1:*; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; object-src 'none'; base-uri 'none'\r\nX-Content-Type-Options: nosniff\r\nConnection: close\r\n\r\n",
        body.len()
    )?;
    stream.write_all(&body)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn health_identifies_the_product() {
        let response = route("GET", "/health", Path::new("."));
        assert_eq!(response.0, 200);
        assert_eq!(
            serde_json::from_slice::<serde_json::Value>(&response.2).unwrap(),
            serde_json::json!({ "project": "rusty-space", "status": "ok" })
        );
    }

    #[test]
    fn unknown_mutation_routes_are_rejected() {
        assert_eq!(route("POST", "/api/session", Path::new(".")).0, 405);
        assert_eq!(route("POST", "/api/input", Path::new(".")).0, 405);
    }

    #[test]
    fn static_routing_rejects_path_traversal() {
        let response = route("GET", "/../../etc/passwd", Path::new("."));
        assert_eq!(response.0, 403);
    }

    #[test]
    fn browser_commands_reject_unknown_fields_and_keep_their_closed_shapes() {
        assert!(
            serde_json::from_str::<BrowserCommand>(
                r#"{"type":"setFlightIntent","generation":1,"throttle":1.0,"turn":0.0,"junk":1}"#
            )
            .is_err()
        );
        assert!(
            serde_json::from_str::<BrowserCommand>(
                r#"{"type":"resetFlight","generation":1,"throttle":0.0}"#
            )
            .is_err()
        );
        let intent = serde_json::from_str::<BrowserCommand>(
            r#"{"type":"setFlightIntent","generation":1,"throttle":1.0,"turn":-1.0}"#,
        )
        .unwrap();
        assert!(matches!(
            intent,
            BrowserCommand::SetFlightIntent {
                generation: 1,
                throttle: 1.0,
                turn: -1.0,
            }
        ));
        assert!(matches!(
            serde_json::from_str::<BrowserCommand>(r#"{"type":"resetFlight","generation":9}"#),
            Ok(BrowserCommand::ResetFlight { generation: 9 })
        ));
    }

    #[test]
    fn malformed_and_unsupported_input_have_typed_rejections() {
        assert!(matches!(
            parse_browser_command("not JSON"),
            Err(CommandRejection {
                code: CommandRejectionCode::MalformedCommand,
                ..
            })
        ));
        assert!(matches!(
            parse_browser_command(r#"{"type":"warp","generation":1}"#),
            Err(CommandRejection {
                code: CommandRejectionCode::UnsupportedCommand,
                ..
            })
        ));
        assert!(matches!(
            parse_browser_command(r#"{"type":"setFlightIntent","generation":1,"throttle":1.0}"#),
            Err(CommandRejection {
                code: CommandRejectionCode::MalformedCommand,
                ..
            })
        ));
        assert!(matches!(
            parse_browser_command(r#"{"type":"resetFlight"}"#),
            Err(CommandRejection {
                code: CommandRejectionCode::MalformedCommand,
                ..
            })
        ));
    }
}
