// Prevents additional console window on Windows in release, DO NOT REMOVE!!
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use std::{
    cell::RefCell,
    sync::{
        mpsc::{self, Sender, TryRecvError},
        Arc, Mutex,
    },
};
use tauri::Manager;

// Learn more about Tauri commands at https://tauri.app/v1/guides/features/command
// #[tauri::command]
// fn greet(name: &str) -> String {
//     format!("Hello, {}! You've been greeted from Rust!", name)
// }

struct State {
    rx: Sender<String>,
}

#[tauri::command]
fn open_file(app: tauri::AppHandle, state: tauri::State<'_, Arc<Mutex<State>>>) {
    eprintln!("open_file");

    let state = Arc::clone(&state);

    tauri::api::dialog::FileDialogBuilder::new()
        .add_filter("Log file", &["log", "txt"])
        .pick_file(move |file_path_opt| {
            let Some(file_path) = file_path_opt else {
                eprintln!("  no file selected");
                return;
            };

            let filename = file_path.to_string_lossy().into_owned();

            {
                let state_lock = state.lock().unwrap();
                state_lock.rx.send(filename).unwrap();
                app.trigger_global("poll", None);
            }
        });
}

#[tauri::command]
fn close_file() {
    eprintln!("close_file");
}

// #[derive(Clone, serde::Serialize)]
// struct TickPayload {
//     value: i32,
// }

fn main() {
    let (rx, tx) = mpsc::channel();
    let state = State { rx };

    tauri::Builder::default()
        .manage(Arc::new(Mutex::new(state)))
        .invoke_handler(tauri::generate_handler![open_file, close_file])
        .setup(move |app| {
            // move: tx

            let main_window = app.get_window("main").unwrap();

            // frontend -> backend messaging
            // let _ = main_window.listen("event-name", |ev| {
            //     println!("got window event-name with payload {:?}", ev.payload());
            // });

            // backend -> frontend messaging
            // thread::spawn(move || {
            //     eprintln!("thread: starting");
            //     for i in 0..5 {
            //         eprintln!("thread: emit({i})");
            //         main_window
            //             .emit("app://listen", &TickPayload { value: i })
            //             .unwrap();
            //         thread::sleep(std::time::Duration::from_millis(1000));
            //     }
            //     eprintln!("thread: stopped");
            // });

            let tx_opt = RefCell::new(Some(tx));
            app.listen_global("poll", move |_| {
                // move: tx_opt
                eprintln!("app: poll");
                loop {
                    let Some(tx) = &mut *tx_opt.borrow_mut() else {
                        return;
                    };
                    match tx.try_recv() {
                        Ok(msg) => {
                            eprintln!("app: msg='{msg}'");
                            main_window.emit("set-filename", msg).unwrap();
                        }
                        Err(TryRecvError::Disconnected) => {
                            tx_opt.take();
                        }
                        Err(TryRecvError::Empty) => {
                            break;
                        }
                    }
                }
                eprintln!("app: poll end");
            });

            Ok(())
        })
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
