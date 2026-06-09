import serial
import nfc
import time
import threading
import sys
from typing import cast, Optional

# --- Constants & Configuration ---
# Zero 2 W: /dev/ttyS0, Zero W: /dev/serial0
PORT_NAME = "/dev/ttyS0" 
BAUD_RATE = 115200
HANDSHAKE_SEND = b"hithere!\n"
HANDSHAKE_RECV = b"cntfluid"
SCAN_DELAY = 1.0  # Seconds between scans for the same card

# --- States ---
STATE_DISCONNECTED = 0
STATE_CONNECTING = 1
STATE_CONNECTED = 2

# --- Globals ---
state = STATE_DISCONNECTED
serial_port: Optional[serial.Serial] = None
nfc_thread: Optional[threading.Thread] = None
shutdown_nfc = threading.Event()
nfc_ready = threading.Event()  # Added synchronization event
last_scan_time = 0

write_lock = threading.Lock()

def safe_serial_write(data: bytes):
    """Safely writes to serial and adds a delay to prevent chunk concatenation in the Windows app."""
    global serial_port
    with write_lock:
        if serial_port and serial_port.is_open:
            serial_port.write(data)
            serial_port.flush()
            time.sleep(0.2)  # Wait 200ms to guarantee the Windows app reads it as a separate chunk

def log(msg: str):
    """Thread-safe logging to stdout."""
    print(f"[{time.strftime('%Y-%m-%d %H:%M:%S')}] {msg}", flush=True)

def nfc_worker():
    """NFC scanning loop running in a separate thread."""
    global last_scan_time
    log("NFC Worker: Starting...")
    
    while not shutdown_nfc.is_set():
        clf = None
        try:
            # "usb"を指定することで、054c:02e1 や 054c:06c3 など、
            # 接続されている対応済みのUSB NFCリーダーを自動認識するようになります。
            clf = nfc.ContactlessFrontend("usb")
            log("NFC Worker: Successfully connected to USB NFC Scanner.")
            nfc_ready.set()  # Signal readiness to main thread
            
            while not shutdown_nfc.is_set():
                # poll with a short timeout to keep the loop responsive to shutdown_nfc
                clf.connect(rdwr={'on-connect': on_nfc_connect}, 
                            terminate=lambda: shutdown_nfc.is_set(), 
                            timeout=0.1)
        except Exception as e:
            nfc_ready.clear()
            log(f"NFC Worker: Error - {e}")
            if not shutdown_nfc.is_set():
                log("NFC Worker: Retrying in 5 seconds...")
                # Sleep in short increments to remain responsive to shutdown signal
                for _ in range(50):
                    if shutdown_nfc.is_set():
                        break
                    time.sleep(0.1)
        finally:
            if clf:
                try:
                    clf.close()
                except:
                    pass
    log("NFC Worker: Stopped.")

def on_nfc_connect(tag):
    """Callback when an NFC tag is detected."""
    global last_scan_time, serial_port
    
    # NFC System/Service codes
    SYS_CODE = 0x81E1
    SERVICE_CODE = 0x300B
    
    current_time = time.time()
    if current_time - last_scan_time < SCAN_DELAY:
        return False

    try:
        # Polling and service selection
        idm, pmm = tag.polling(system_code=SYS_CODE)
        tag.idm, tag.pmm, tag.sys = idm, pmm, SYS_CODE
        sc = nfc.tag.tt3.ServiceCode(SERVICE_CODE >> 6, SERVICE_CODE & 0x3F)
        bc = nfc.tag.tt3.BlockCode(0, service=0)
        
        # Read student number (ID)
        data = cast(bytearray, tag.read_without_encryption([sc], [bc]))
        student_num = data.rstrip(b'\x00').decode("shift_jis")
        
        log(f"NFC: ID Scanned -> {student_num}")
        
        # Send to serial port if connected
        safe_serial_write(student_num.encode('utf-8'))
        
        last_scan_time = current_time
    except Exception as e:
        log(f"NFC: Error reading tag - {e}")
    
    return True

def reset_connection():
    """Stops all sub-tasks and closes ports to prepare for a fresh connection."""
    global state, serial_port, nfc_thread, shutdown_nfc
    log("System: Resetting connection components...")
    
    # 1. Signal and Wait for NFC thread to stop
    shutdown_nfc.set()
    nfc_ready.clear()
    if nfc_thread and nfc_thread.is_alive():
        nfc_thread.join(timeout=2.0)
    nfc_thread = None
    
    # 2. Close Serial Port
    if serial_port:
        try:
            if serial_port.is_open:
                serial_port.close()
        except:
            pass
        serial_port = None
    
    state = STATE_DISCONNECTED

def main_loop():
    """Main application loop managing the connection state machine."""
    global state, serial_port, nfc_thread, shutdown_nfc
    
    log("System: Fluid Console Service Started.")
    
    while True:
        try:
            if state == STATE_DISCONNECTED:
                # Attempting to open serial port
                try:
                    serial_port = serial.Serial(port=PORT_NAME, baudrate=BAUD_RATE, timeout=0.1)
                    state = STATE_CONNECTING
                    log(f"Serial: Opened {PORT_NAME}. Waiting for handshake '{HANDSHAKE_RECV.decode()}'...")
                except (serial.SerialException, OSError) as e:
                    # Log and wait before retry
                    log(f"Serial: Could not open port - {e}")
                    time.sleep(2)
            
            elif state == STATE_CONNECTING:
                # Look for the handshake string in the incoming buffer
                if serial_port and serial_port.in_waiting > 0:
                    data = serial_port.read(serial_port.in_waiting)
                    if HANDSHAKE_RECV in data:
                        # Reply with handshake response
                        safe_serial_write(HANDSHAKE_SEND)
                        log("Serial: Handshake received! Response sent.")
                        
                        # Start NFC background worker
                        shutdown_nfc.clear()
                        nfc_ready.clear()
                        nfc_thread = threading.Thread(target=nfc_worker, daemon=True)
                        nfc_thread.start()
                        
                        # Wait for NFC to be ready before declaring operational
                        log("System: Waiting for NFC device initialization...")
                        if nfc_ready.wait(timeout=8.0):
                            state = STATE_CONNECTED
                            log("System: Connected and operational.")
                        else:
                            log("System: NFC initialization timeout (device may be missing). Retrying in background.")
                            state = STATE_CONNECTED  # Transition anyway to allow PING/PONG
                else:
                    time.sleep(0.2)

            elif state == STATE_CONNECTED:
                # In connected state, monitor for commands from the PC/Console
                if serial_port and serial_port.in_waiting > 0:
                    # Read available data and check for specific command strings
                    raw_cmds = serial_port.read(serial_port.in_waiting)
                    
                    if b"111111111" in raw_cmds:  # 9 digits from Windows app
                        log("Command: Reconnect signal received.")
                        reset_connection()
                    elif b"222222222" in raw_cmds:  # 9 digits from Windows app
                        log("Command: Shutdown signal received.")
                        reset_connection()
                        log("System: Exiting...")
                        sys.exit(0)
                else:
                    # Idle sleep to prevent high CPU usage
                    time.sleep(0.1)

        except (serial.SerialException, OSError) as e:
            log(f"System: Communication error - {e}")
            reset_connection()
            time.sleep(1)
        except Exception as e:
            log(f"System: Unexpected error - {e}")
            reset_connection()
            time.sleep(1)

if __name__ == "__main__":
    try:
        main_loop()
    except KeyboardInterrupt:
        log("System: Interrupted by user (KeyboardInterrupt).")
        reset_connection()
        sys.exit(0)
