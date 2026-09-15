fn main() {
    if std::env::var("CARGO_CFG_TARGET_OS").unwrap_or_default() == "windows" {
        let mut res = winres::WindowsResource::new();
        res.set_icon("../app/favicon.ico");
        res.set("FileDescription", "G-Helper M4 Key Background Watcher");
        res.set("ProductName", "G-Helper");
        res.set("OriginalFilename", "GHelperWatcher.exe");
        let _ = res.compile();
    }
}
