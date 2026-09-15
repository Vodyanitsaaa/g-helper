#![windows_subsystem = "windows"]

use std::ffi::c_void;
use std::fs::{create_dir_all, OpenOptions};
use std::io::Write;
use std::path::{Path, PathBuf};
use std::ptr::null_mut;

type HANDLE = *mut c_void;
type HWND = *mut c_void;
type HKEY = *mut c_void;
type BOOL = i32;

const TRUE: BOOL = 1;
const FALSE: BOOL = 0;
const INVALID_HANDLE_VALUE: HANDLE = -1isize as HANDLE;

const ERROR_ALREADY_EXISTS: u32 = 183;
const ERROR_IO_PENDING: u32 = 997;
const WAIT_OBJECT_0: u32 = 0x00000000;
const WAIT_TIMEOUT: u32 = 0x00000102;

const GENERIC_READ: u32 = 0x80000000;
const GENERIC_WRITE: u32 = 0x40000000;
const FILE_SHARE_READ: u32 = 0x00000001;
const FILE_SHARE_WRITE: u32 = 0x00000002;
const OPEN_EXISTING: u32 = 3;
const FILE_FLAG_OVERLAPPED: u32 = 0x40000000;

const DIGCF_PRESENT: u32 = 0x00000002;
const DIGCF_DEVICEINTERFACE: u32 = 0x00000010;

const TH32CS_SNAPPROCESS: u32 = 0x00000002;

const HKEY_CURRENT_USER: HKEY = 0x80000001usize as HKEY;
const KEY_SET_VALUE: u32 = 0x0002;
const REG_SZ: u32 = 1;

const SW_SHOWNORMAL: i32 = 1;
const MB_OK: u32 = 0x00000000;
const MB_ICONINFORMATION: u32 = 0x00000040;

const ASUS_VID: u16 = 0x0B05;
const REPORT_ID: u8 = 0x5A;
const KEY_M4: u8 = 56;

#[repr(C)]
#[derive(Copy, Clone, Debug, PartialEq, Eq)]
pub struct GUID {
    pub data1: u32,
    pub data2: u16,
    pub data3: u16,
    pub data4: [u8; 8],
}

#[repr(C)]
pub struct SP_DEVICE_INTERFACE_DATA {
    pub cb_size: u32,
    pub interface_class_guid: GUID,
    pub flags: u32,
    pub reserved: usize,
}

#[repr(C)]
pub struct HIDD_ATTRIBUTES {
    pub size: u32,
    pub vendor_id: u16,
    pub product_id: u16,
    pub version_number: u16,
}

#[repr(C)]
pub struct HIDP_CAPS {
    pub usage: u16,
    pub usage_page: u16,
    pub input_report_byte_length: u16,
    pub output_report_byte_length: u16,
    pub feature_report_byte_length: u16,
    pub reserved: [u16; 17],
    pub number_link_collection_nodes: u16,
    pub number_input_button_caps: u16,
    pub number_input_value_caps: u16,
    pub number_input_data_indices: u16,
    pub number_output_button_caps: u16,
    pub number_output_value_caps: u16,
    pub number_output_data_indices: u16,
    pub number_feature_button_caps: u16,
    pub number_feature_value_caps: u16,
    pub number_feature_data_indices: u16,
}

#[repr(C)]
pub struct OVERLAPPED {
    pub internal: usize,
    pub internal_high: usize,
    pub offset: u32,
    pub offset_high: u32,
    pub h_event: HANDLE,
}

#[repr(C)]
pub struct PROCESSENTRY32W {
    pub dw_size: u32,
    pub cnt_usage: u32,
    pub th32_process_id: u32,
    pub th32_default_heap_id: usize,
    pub th32_module_id: u32,
    pub cnt_threads: u32,
    pub th32_parent_process_id: u32,
    pub pc_pri_class_base: i32,
    pub dw_flags: u32,
    pub sz_exe_file: [u16; 260],
}

#[repr(C)]
pub struct SYSTEMTIME {
    pub w_year: u16,
    pub w_month: u16,
    pub w_day_of_week: u16,
    pub w_day: u16,
    pub w_hour: u16,
    pub w_minute: u16,
    pub w_second: u16,
    pub w_milliseconds: u16,
}

#[link(name = "hid")]
unsafe extern "system" {
    fn HidD_GetHidGuid(hid_guid: *mut GUID);
    fn HidD_GetAttributes(hid_device_object: HANDLE, attributes: *mut HIDD_ATTRIBUTES) -> BOOL;
    fn HidD_GetPreparsedData(hid_device_object: HANDLE, preparsed_data: *mut *mut c_void) -> BOOL;
    fn HidD_FreePreparsedData(preparsed_data: *mut c_void) -> BOOL;
    fn HidP_GetCaps(preparsed_data: *mut c_void, capabilities: *mut HIDP_CAPS) -> i32;
    fn HidD_SetFeature(hid_device_object: HANDLE, report_buffer: *const c_void, report_buffer_length: u32) -> BOOL;
}

#[link(name = "setupapi")]
unsafe extern "system" {
    fn SetupDiGetClassDevsW(
        class_guid: *const GUID,
        enumerator: *const u16,
        hwnd_parent: HWND,
        flags: u32,
    ) -> *mut c_void;
    fn SetupDiEnumDeviceInterfaces(
        device_info_set: *mut c_void,
        device_info_data: *mut c_void,
        interface_class_guid: *const GUID,
        member_index: u32,
        device_interface_data: *mut SP_DEVICE_INTERFACE_DATA,
    ) -> BOOL;
    fn SetupDiGetDeviceInterfaceDetailW(
        device_info_set: *mut c_void,
        device_interface_data: *mut SP_DEVICE_INTERFACE_DATA,
        device_interface_detail_data: *mut u8,
        device_interface_detail_data_size: u32,
        required_size: *mut u32,
        device_info_data: *mut c_void,
    ) -> BOOL;
    fn SetupDiDestroyDeviceInfoList(device_info_set: *mut c_void) -> BOOL;
}

#[link(name = "kernel32")]
unsafe extern "system" {
    fn CreateFileW(
        lp_file_name: *const u16,
        dw_desired_access: u32,
        dw_share_mode: u32,
        lp_security_attributes: *mut c_void,
        dw_creation_disposition: u32,
        dw_flags_and_attributes: u32,
        h_template_file: HANDLE,
    ) -> HANDLE;
    fn CloseHandle(h_object: HANDLE) -> BOOL;
    fn ReadFile(
        h_file: HANDLE,
        lp_buffer: *mut c_void,
        n_number_of_bytes_to_read: u32,
        lp_number_of_bytes_read: *mut u32,
        lp_overlapped: *mut OVERLAPPED,
    ) -> BOOL;
    fn CreateEventW(
        lp_event_attributes: *mut c_void,
        b_manual_reset: BOOL,
        b_initial_state: BOOL,
        lp_name: *const u16,
    ) -> HANDLE;
    fn WaitForSingleObject(h_handle: HANDLE, dw_milliseconds: u32) -> u32;
    fn GetOverlappedResult(
        h_file: HANDLE,
        lp_overlapped: *mut OVERLAPPED,
        lp_number_of_bytes_transferred: *mut u32,
        b_wait: BOOL,
    ) -> BOOL;
    fn CancelIo(h_file: HANDLE) -> BOOL;
    fn CreateMutexW(
        lp_mutex_attributes: *mut c_void,
        b_initial_owner: BOOL,
        lp_name: *const u16,
    ) -> HANDLE;
    fn GetLastError() -> u32;
    fn Sleep(dw_milliseconds: u32);
    fn CreateToolhelp32Snapshot(dw_flags: u32, th32_process_id: u32) -> HANDLE;
    fn Process32FirstW(h_snapshot: HANDLE, lppe: *mut PROCESSENTRY32W) -> BOOL;
    fn Process32NextW(h_snapshot: HANDLE, lppe: *mut PROCESSENTRY32W) -> BOOL;
    fn GetCurrentProcess() -> HANDLE;
    fn SetProcessWorkingSetSize(h_process: HANDLE, dw_minimum_working_set_size: usize, dw_maximum_working_set_size: usize) -> BOOL;
    fn GetModuleFileNameW(h_module: HANDLE, lp_filename: *mut u16, n_size: u32) -> u32;
    fn GetLocalTime(lp_system_time: *mut SYSTEMTIME);
}

#[link(name = "user32")]
unsafe extern "system" {
    fn MessageBoxW(h_wnd: HWND, lp_text: *const u16, lp_caption: *const u16, u_type: u32) -> i32;
}

#[link(name = "shell32")]
unsafe extern "system" {
    fn ShellExecuteW(
        hwnd: HWND,
        lp_operation: *const u16,
        lp_file: *const u16,
        lp_parameters: *const u16,
        lp_directory: *const u16,
        n_show_cmd: i32,
    ) -> usize;
}

#[link(name = "advapi32")]
unsafe extern "system" {
    fn RegOpenKeyExW(
        h_key: HKEY,
        lp_sub_key: *const u16,
        ul_options: u32,
        sam_desired: u32,
        phk_result: *mut HKEY,
    ) -> i32;
    fn RegSetValueExW(
        h_key: HKEY,
        lp_value_name: *const u16,
        reserved: u32,
        dw_type: u32,
        lp_data: *const u8,
        cb_data: u32,
    ) -> i32;
    fn RegDeleteValueW(h_key: HKEY, lp_value_name: *const u16) -> i32;
    fn RegCloseKey(h_key: HKEY) -> i32;
}

fn to_wide(s: &str) -> Vec<u16> {
    s.encode_utf16().chain(std::iter::once(0)).collect()
}

fn get_timestamp() -> String {
    unsafe {
        let mut st = std::mem::zeroed::<SYSTEMTIME>();
        GetLocalTime(&mut st);
        format!(
            "{:04}-{:02}-{:02} {:02}:{:02}:{:02}.{:03}",
            st.w_year, st.w_month, st.w_day, st.w_hour, st.w_minute, st.w_second, st.w_milliseconds
        )
    }
}

fn log_msg(msg: &str) {
    let appdata = match std::env::var("APPDATA") {
        Ok(v) => v,
        Err(_) => return,
    };
    let dir = Path::new(&appdata).join("GHelper");
    let _ = create_dir_all(&dir);
    let log_path = dir.join("watcher.log");

    if let Ok(meta) = std::fs::metadata(&log_path) {
        if meta.len() > 100 * 1024 {
            let _ = std::fs::remove_file(&log_path);
        }
    }

    if let Ok(mut f) = OpenOptions::new().create(true).append(true).open(&log_path) {
        let now = get_timestamp();
        let _ = writeln!(f, "{} {}", now, msg);
    }
}

fn is_ghelper_running() -> bool {
    unsafe {
        let snap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if snap == INVALID_HANDLE_VALUE {
            return false;
        }

        let mut entry = PROCESSENTRY32W {
            dw_size: std::mem::size_of::<PROCESSENTRY32W>() as u32,
            cnt_usage: 0,
            th32_process_id: 0,
            th32_default_heap_id: 0,
            th32_module_id: 0,
            cnt_threads: 0,
            th32_parent_process_id: 0,
            pc_pri_class_base: 0,
            dw_flags: 0,
            sz_exe_file: [0; 260],
        };

        let mut found = false;
        if Process32FirstW(snap, &mut entry) == TRUE {
            loop {
                let name = String::from_utf16_lossy(&entry.sz_exe_file);
                let trimmed = name.trim_matches(char::from(0)).to_lowercase();
                if trimmed == "ghelper.exe" {
                    found = true;
                    break;
                }
                if Process32NextW(snap, &mut entry) == FALSE {
                    break;
                }
            }
        }
        CloseHandle(snap);
        found
    }
}

fn get_exe_path() -> PathBuf {
    let mut buf = [0u16; 1024];
    let len = unsafe { GetModuleFileNameW(null_mut(), buf.as_mut_ptr(), buf.len() as u32) };
    if len > 0 {
        PathBuf::from(String::from_utf16_lossy(&buf[..len as usize]))
    } else {
        std::env::current_exe().unwrap_or_else(|_| PathBuf::from("GHelperWatcher.exe"))
    }
}

fn get_ghelper_path() -> PathBuf {
    let my_dir = get_exe_path().parent().map(|p| p.to_path_buf()).unwrap_or_default();
    let same_dir = my_dir.join("GHelper.exe");
    if same_dir.exists() {
        return same_dir;
    }
    let deployed = PathBuf::from(r"D:\softwares\G-Helper\GHelper.exe");
    if deployed.exists() {
        return deployed;
    }
    same_dir
}

fn trim_memory() {
    unsafe {
        SetProcessWorkingSetSize(GetCurrentProcess(), usize::MAX, usize::MAX);
    }
}

struct HidDeviceInfo {
    path: Vec<u16>,
    path_str: String,
    feature_len: u16,
    input_len: u16,
}

fn find_asus_input_device() -> Option<HidDeviceInfo> {
    unsafe {
        let mut hid_guid = GUID { data1: 0, data2: 0, data3: 0, data4: [0; 8] };
        HidD_GetHidGuid(&mut hid_guid);

        let dev_info = SetupDiGetClassDevsW(
            &hid_guid,
            null_mut(),
            null_mut(),
            DIGCF_PRESENT | DIGCF_DEVICEINTERFACE,
        );

        if dev_info.is_null() || dev_info == INVALID_HANDLE_VALUE {
            return None;
        }

        let mut index = 0;
        let mut if_data = SP_DEVICE_INTERFACE_DATA {
            cb_size: std::mem::size_of::<SP_DEVICE_INTERFACE_DATA>() as u32,
            interface_class_guid: hid_guid,
            flags: 0,
            reserved: 0,
        };

        let mut matched: Option<HidDeviceInfo> = None;

        while SetupDiEnumDeviceInterfaces(dev_info, null_mut(), &hid_guid, index, &mut if_data) == TRUE {
            index += 1;
            let mut req_size = 0u32;
            SetupDiGetDeviceInterfaceDetailW(
                dev_info,
                &mut if_data,
                null_mut(),
                0,
                &mut req_size,
                null_mut(),
            );

            if req_size == 0 {
                continue;
            }

            let mut detail_buf = vec![0u8; req_size as usize];
            #[cfg(target_pointer_width = "64")]
            let cb_size: u32 = 8;
            #[cfg(target_pointer_width = "32")]
            let cb_size: u32 = 6;

            *(detail_buf.as_mut_ptr() as *mut u32) = cb_size;

            if SetupDiGetDeviceInterfaceDetailW(
                dev_info,
                &mut if_data,
                detail_buf.as_mut_ptr(),
                req_size,
                null_mut(),
                null_mut(),
            ) == TRUE {
                let path_ptr = detail_buf.as_ptr().add(4) as *const u16;
                let mut path_len = 0;
                while *path_ptr.add(path_len) != 0 {
                    path_len += 1;
                }
                let path_vec: Vec<u16> = std::slice::from_raw_parts(path_ptr, path_len + 1).to_vec();
                let path_str = String::from_utf16_lossy(&path_vec[..path_len]);

                let handle = CreateFileW(
                    path_ptr,
                    GENERIC_READ | GENERIC_WRITE,
                    FILE_SHARE_READ | FILE_SHARE_WRITE,
                    null_mut(),
                    OPEN_EXISTING,
                    0,
                    null_mut(),
                );

                if handle != INVALID_HANDLE_VALUE {
                    let mut attr = HIDD_ATTRIBUTES {
                        size: std::mem::size_of::<HIDD_ATTRIBUTES>() as u32,
                        vendor_id: 0,
                        product_id: 0,
                        version_number: 0,
                    };

                    if HidD_GetAttributes(handle, &mut attr) == TRUE && attr.vendor_id == ASUS_VID {
                        let mut prep_data: *mut c_void = null_mut();
                        if HidD_GetPreparsedData(handle, &mut prep_data) == TRUE {
                            let mut caps = std::mem::zeroed::<HIDP_CAPS>();
                            if HidP_GetCaps(prep_data, &mut caps) >= 0 && caps.feature_report_byte_length > 0 {
                                // Test handshake
                                let mut buf = vec![0u8; caps.feature_report_byte_length as usize];
                                buf[0] = REPORT_ID;
                                let tag = b"ASUS Tech.Inc.";
                                buf[1..1 + tag.len()].copy_from_slice(tag);

                                if HidD_SetFeature(handle, buf.as_ptr() as *const c_void, buf.len() as u32) == TRUE {
                                    matched = Some(HidDeviceInfo {
                                        path: path_vec,
                                        path_str,
                                        feature_len: caps.feature_report_byte_length,
                                        input_len: caps.input_report_byte_length,
                                    });
                                }
                            }
                            HidD_FreePreparsedData(prep_data);
                        }
                    }
                    CloseHandle(handle);
                }
            }

            if matched.is_some() {
                break;
            }
        }

        SetupDiDestroyDeviceInfoList(dev_info);
        matched
    }
}

fn install_task() {
    let exe = get_exe_path();
    let exe_str = exe.to_str().unwrap_or("");
    let quoted_exe = format!("\"{}\"", exe_str);

    unsafe {
        let subkey = to_wide(r"Software\Microsoft\Windows\CurrentVersion\Run");
        let mut hkey = null_mut();
        if RegOpenKeyExW(HKEY_CURRENT_USER, subkey.as_ptr(), 0, KEY_SET_VALUE, &mut hkey) == 0 {
            let val_name = to_wide("GHelperWatcher");
            let val_data = to_wide(&quoted_exe);
            RegSetValueExW(
                hkey,
                val_name.as_ptr(),
                0,
                REG_SZ,
                val_data.as_ptr() as *const u8,
                (val_data.len() * 2) as u32,
            );
            RegCloseKey(hkey);
        }

        let cmd = format!(
            "schtasks /create /tn \"GHelperWatcher\" /tr \"\\\"{}\\\"\" /sc onlogon /rl highest /f",
            exe_str
        );
        let _ = std::process::Command::new("cmd").args(["/c", &cmd]).output();

        let title = to_wide("安装成功");
        let msg = to_wide(&format!("GHelperWatcher 已成功设置为开机自启！\n\n程序路径: {}", exe_str));
        MessageBoxW(null_mut(), msg.as_ptr(), title.as_ptr(), MB_OK | MB_ICONINFORMATION);
    }
}

fn uninstall_task() {
    unsafe {
        let subkey = to_wide(r"Software\Microsoft\Windows\CurrentVersion\Run");
        let mut hkey = null_mut();
        if RegOpenKeyExW(HKEY_CURRENT_USER, subkey.as_ptr(), 0, KEY_SET_VALUE, &mut hkey) == 0 {
            let val_name = to_wide("GHelperWatcher");
            RegDeleteValueW(hkey, val_name.as_ptr());
            RegCloseKey(hkey);
        }

        let _ = std::process::Command::new("cmd").args(["/c", "schtasks /delete /tn \"GHelperWatcher\" /f"]).output();

        let title = to_wide("卸载成功");
        let msg = to_wide("GHelperWatcher 自启已移除。");
        MessageBoxW(null_mut(), msg.as_ptr(), title.as_ptr(), MB_OK | MB_ICONINFORMATION);
    }
}

fn show_status() {
    let running = is_ghelper_running();
    let title = to_wide("状态查询");
    let msg = to_wide(&format!("GHelper 运行状态: {}", if running { "正在运行中" } else { "未在运行 (Watcher 监听中)" }));
    unsafe {
        MessageBoxW(null_mut(), msg.as_ptr(), title.as_ptr(), MB_OK | MB_ICONINFORMATION);
    }
}

fn main() {
    let args: Vec<String> = std::env::args().collect();
    if args.len() > 1 {
        let arg = args[1].trim_start_matches('-').trim_start_matches('/').to_lowercase();
        match arg.as_str() {
            "install" | "i" => {
                install_task();
                return;
            }
            "uninstall" | "u" => {
                uninstall_task();
                return;
            }
            "status" | "s" => {
                show_status();
                return;
            }
            _ => {}
        }
    }

    // Single instance mutex
    let mutex_name = to_wide(r"Global\GHelperWatcher_SingleInstance_Mutex");
    let _h_mutex = unsafe { CreateMutexW(null_mut(), TRUE, mutex_name.as_ptr()) };
    if unsafe { GetLastError() } == ERROR_ALREADY_EXISTS {
        return;
    }

    log_msg("=== GHelperWatcher (Native Win32) started ===");
    trim_memory();

    loop {
        // If GHelper is currently running, pause listening and wait
        if is_ghelper_running() {
            log_msg("GHelper is running. Pausing HID listener...");
            while is_ghelper_running() {
                trim_memory();
                unsafe { Sleep(1000) };
            }
            log_msg("GHelper exited. Waiting 1.5s before taking over HID...");
            unsafe { Sleep(1500) };
            continue;
        }

        // Find ROG HID input device
        let dev = match find_asus_input_device() {
            Some(d) => d,
            None => {
                unsafe { Sleep(2000) };
                continue;
            }
        };

        log_msg(&format!("Opening HID device: {}", dev.path_str));

        unsafe {
            let handle = CreateFileW(
                dev.path.as_ptr(),
                GENERIC_READ | GENERIC_WRITE,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                null_mut(),
                OPEN_EXISTING,
                FILE_FLAG_OVERLAPPED,
                null_mut(),
            );

            if handle == INVALID_HANDLE_VALUE {
                log_msg("Failed to open HID device with OVERLAPPED flag");
                Sleep(2000);
                continue;
            }

            // Send handshake
            let mut handshake_buf = vec![0u8; dev.feature_len as usize];
            handshake_buf[0] = REPORT_ID;
            let tag = b"ASUS Tech.Inc.";
            handshake_buf[1..1 + tag.len()].copy_from_slice(tag);
            HidD_SetFeature(handle, handshake_buf.as_ptr() as *const c_void, handshake_buf.len() as u32);

            let h_event = CreateEventW(null_mut(), TRUE, FALSE, null_mut());
            let mut in_buf = vec![0u8; dev.input_len.max(64) as usize];

            log_msg(&format!("Listening for M4 key on {}", dev.path_str));
            trim_memory();

            loop {
                // Check if GHelper launched elsewhere
                if is_ghelper_running() {
                    log_msg("GHelper detected running. Releasing HID handle...");
                    break;
                }

                let mut overlapped: OVERLAPPED = std::mem::zeroed();
                overlapped.h_event = h_event;

                let mut bytes_read = 0u32;
                let read_ok = ReadFile(
                    handle,
                    in_buf.as_mut_ptr() as *mut c_void,
                    in_buf.len() as u32,
                    &mut bytes_read,
                    &mut overlapped,
                );

                let mut report_received = false;

                if read_ok == TRUE {
                    report_received = true;
                } else {
                    let err = GetLastError();
                    if err == ERROR_IO_PENDING {
                        let wait_res = WaitForSingleObject(h_event, 1000);
                        if wait_res == WAIT_OBJECT_0 {
                            if GetOverlappedResult(handle, &mut overlapped, &mut bytes_read, FALSE) == TRUE {
                                report_received = true;
                            }
                        } else if wait_res == WAIT_TIMEOUT {
                            CancelIo(handle);
                        }
                    } else {
                        log_msg(&format!("ReadFile error: {}", err));
                        break;
                    }
                }

                if report_received && bytes_read >= 2 {
                    if in_buf[0] == REPORT_ID && in_buf[1] == KEY_M4 {
                        log_msg(">>> M4 key pressed! Releasing HID and launching GHelper...");
                        CancelIo(handle);
                        CloseHandle(h_event);
                        CloseHandle(handle);

                        let ghelper_exe = get_ghelper_path();
                        if ghelper_exe.exists() {
                            let wide_exe = to_wide(ghelper_exe.to_str().unwrap_or(""));
                            let wide_open = to_wide("open");
                            ShellExecuteW(
                                null_mut(),
                                wide_open.as_ptr(),
                                wide_exe.as_ptr(),
                                null_mut(),
                                null_mut(),
                                SW_SHOWNORMAL,
                            );
                            log_msg(&format!("Successfully launched: {:?}", ghelper_exe));
                        } else {
                            log_msg(&format!("GHelper.exe not found at: {:?}", ghelper_exe));
                        }

                        Sleep(3000);
                        break;
                    }
                }
            }

            CancelIo(handle);
            CloseHandle(h_event);
            CloseHandle(handle);
        }
    }
}
