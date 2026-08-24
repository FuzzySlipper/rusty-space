//! browser-host: a minimal HTTP + WebSocket development adapter over the live
//! flight runtime. It serves the built web shell, owns the fixed 60 Hz loop,
//! accepts typed turn/thrust input intents, and pushes projected frames.

use std::io::{ErrorKind, Read, Write};
use std::net::{SocketAddr, TcpListener, TcpStream};
use std::path::{Component, Path, PathBuf};
use std::sync::atomic::{AtomicU64, Ordering};
use std::sync::{Arc, Mutex};
use std::time::{Duration, Instant};

use rusty_engine::render_model::RenderFrameDiff;
use rusty_space_gameplay::{FlightCommand, compile_ship_handling};
use rusty_space_runtime::{FIXED_STEP_SECONDS, FlightReadout, FlightRuntime, ship_frame_diff};
use serde::{Deserialize, Serialize};
use tungstenite::{Message, accept};

const DEFAULT_ADDRESS: &str = "127.0.0.1:8787";
const SESSION_PATH: &str = "/api/session";

struct Shared {
    runtime: Mutex<FlightRuntime>,
    command: Mutex<FlightCommand>,
    frame: Mutex<Option<String>>,
    frame_sequence: AtomicU64,
}

impl Shared {
    fn new(runtime: FlightRuntime) -> Self {
        Self {
            runtime: Mutex::new(runtime),
            command: Mutex::new(FlightCommand {
                throttle: 0.0,
                turn: 0.0,
            }),
            frame: Mutex::new(None),
            frame_sequence: AtomicU64::new(0),
        }
    }
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct InputIntent {
    throttle: f64,
    turn: f64,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
struct ServerUpdate {
    frame: RenderFrameDiff,
    readout: FlightReadout,
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

    let handling =
        compile_ship_handling(&std::fs::read(&arguments.handling).unwrap_or_else(|error| {
            panic!(
                "cannot read ship handling package {}: {error}",
                arguments.handling.display()
            )
        }))
        .unwrap_or_else(|error| panic!("ship handling admission failed: {error}"));
    let runtime =
        FlightRuntime::spawn(handling).unwrap_or_else(|error| panic!("spawn failed: {error}"));
    let shared = Arc::new(Shared::new(runtime));
    start_driver(&shared);

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
                let shared = Arc::clone(&shared);
                let dist = dist.clone();
                std::thread::spawn(move || handle_connection(stream, &shared, &dist));
            }
            Err(error) => eprintln!("browser-host accept error: {error}"),
        }
    }
}

/// The fixed 60 Hz loop. It owns the only authority over simulation time and
/// publishes each tick's projected frame for connected sessions.
fn start_driver(shared: &Arc<Shared>) {
    let shared = Arc::clone(shared);
    std::thread::spawn(move || {
        let step = Duration::from_secs_f64(FIXED_STEP_SECONDS);
        let mut previous = Instant::now();
        let mut accumulator = Duration::ZERO;
        let mut tick = 0_u64;
        loop {
            std::thread::sleep(Duration::from_millis(1));
            let now = Instant::now();
            accumulator += now.saturating_duration_since(previous);
            previous = now;
            while accumulator >= step {
                accumulator -= step;
                tick_once(&shared, &mut tick);
            }
            if accumulator > step * 4 {
                accumulator = Duration::ZERO;
            }
        }
    });
}

fn tick_once(shared: &Shared, tick: &mut u64) {
    let command = *shared.command.lock().expect("command lock");
    let mut runtime = shared.runtime.lock().expect("runtime lock");
    match runtime.tick(command) {
        Ok(readout) => {
            let create = *tick == 0;
            let update = ServerUpdate {
                frame: ship_frame_diff(&readout, create),
                readout,
            };
            let encoded = serde_json::to_string(&update).expect("encode server update");
            *shared.frame.lock().expect("frame lock") = Some(encoded);
            shared.frame_sequence.fetch_add(1, Ordering::Relaxed);
            *tick += 1;
        }
        Err(error) => eprintln!("browser-host flight tick error: {error}"),
    }
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

fn handle_connection(mut stream: TcpStream, shared: &Arc<Shared>, dist: &Path) {
    if session_upgrade_requested(&stream) {
        run_session(stream, Arc::clone(shared));
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

fn run_session(stream: TcpStream, shared: Arc<Shared>) {
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

    let mut published_sequence = 0_u64;
    loop {
        // Drain up to a bounded batch of incoming input intents.
        let mut drained = 0;
        while drained < 32 {
            match websocket.read() {
                Ok(Message::Text(text)) => {
                    if let Ok(intent) = serde_json::from_str::<InputIntent>(&text) {
                        *shared.command.lock().expect("command lock") = FlightCommand {
                            throttle: intent.throttle.clamp(0.0, 1.0),
                            turn: intent.turn.clamp(-1.0, 1.0),
                        };
                    }
                }
                Ok(Message::Close(frame)) => {
                    let _ = websocket.close(frame);
                    return;
                }
                Ok(Message::Ping(payload)) => {
                    if websocket.send(Message::Pong(payload)).is_err() {
                        return;
                    }
                }
                Ok(Message::Pong(_)) | Ok(Message::Frame(_)) | Ok(Message::Binary(_)) => {}
                Err(error) if websocket_would_block(&error) => break,
                Err(_) => return,
            }
            drained += 1;
        }

        // Push the latest frame when it has advanced.
        let sequence = shared.frame_sequence.load(Ordering::Relaxed);
        if sequence != published_sequence
            && let Some(encoded) = shared.frame.lock().expect("frame lock").clone()
        {
            if websocket.send(Message::Text(encoded.into())).is_err() {
                return;
            }
            published_sequence = sequence;
        }
    }
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
    fn input_intent_rejects_unknown_fields() {
        assert!(
            serde_json::from_str::<InputIntent>(r#"{"throttle":1.0,"turn":0.0,"junk":1}"#).is_err()
        );
        let intent =
            serde_json::from_str::<InputIntent>(r#"{"throttle":1.0,"turn":-1.0}"#).unwrap();
        assert_eq!(intent.throttle, 1.0);
        assert_eq!(intent.turn, -1.0);
    }
}
