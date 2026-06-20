import serial
import nfc
import time
import threading
import sys
import atexit
import os
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

# --- Logging Helper ---
def log(msg: str):
    """Thread-safe logging to stdout."""
    print(f"[{time.strftime('%Y-%m-%d %H:%M:%S')}] {msg}", flush=True)

# --- GPIO/LED Configuration ---
LED_PIN = 18
has_gpio = False
gpio_mode = None
led_device = None

try:
    import RPi.GPIO as GPIO
    GPIO.setmode(GPIO.BCM)
    GPIO.setup(LED_PIN, GPIO.OUT)
    GPIO.output(LED_PIN, GPIO.LOW)
    has_gpio = True
    gpio_mode = "rpi"
    log("GPIO: RPi.GPIO initialized. LED pin: 18")
except ImportError:
    try:
        from gpiozero import LED
        led_device = LED(LED_PIN)
        has_gpio = True
        gpio_mode = "gpiozero"
        log("GPIO: gpiozero initialized. LED pin: 18")
    except ImportError:
        log("GPIO: RPi.GPIO or gpiozero not found. Running in mock/simulation mode.")

# --- Servo Configuration ---
SERVO_PIN = 12
has_servo = False
pi = None

try:
    import pigpio
    pi = pigpio.pi()
    if pi.connected:
        has_servo = True
        log("Servo: pigpio initialized. Servo pin: 12")
    else:
        log("Servo: pigpiod daemon not running. Please start it using 'sudo systemctl start pigpiod'. Running in mock mode.")
except Exception as e:
    log(f"Servo: pigpio initialization failed ({e}). Running in mock/simulation mode.")

def cleanup_gpio():
    global pi
    if has_servo and pi:
        try:
            pi.set_servo_pulsewidth(SERVO_PIN, 0) # Stop servo pulse
            pi.stop()
            log("Servo: pigpio connection stopped.")
        except:
            pass
    if has_gpio and gpio_mode == "rpi":
        try:
            GPIO.cleanup()
            log("GPIO: Cleaned up pins.")
        except Exception as e:
            log(f"GPIO: Error during cleanup - {e}")

atexit.register(cleanup_gpio)

# --- Shutdown Button Configuration ---
SHUTDOWN_PIN = 26
has_shutdown_btn = False
shutdown_btn_device = None

if has_gpio:
    try:
        if gpio_mode == "rpi":
            GPIO.setup(SHUTDOWN_PIN, GPIO.IN, pull_up_down=GPIO.PUD_UP)
            has_shutdown_btn = True
            log("Shutdown Button: Initialized on BCM 26 (pull-up) using RPi.GPIO.")
        elif gpio_mode == "gpiozero":
            from gpiozero import Button
            shutdown_btn_device = Button(SHUTDOWN_PIN, pull_up=True)
            has_shutdown_btn = True
            log("Shutdown Button: Initialized on BCM 26 (pull-up) using gpiozero.")
    except Exception as e:
        log(f"Shutdown Button: Initialization failed - {e}")

# LED pattern control
led_flash_active = False
led_flash_end_time = 0.0

def set_led_hardware(on: bool):
    if not has_gpio:
        return
    try:
        if gpio_mode == "rpi":
            GPIO.output(LED_PIN, GPIO.HIGH if on else GPIO.LOW)
        elif gpio_mode == "gpiozero" and led_device:
            if on:
                led_device.on()
            else:
                led_device.off()
    except Exception as e:
        log(f"GPIO: Error writing to LED pin - {e}")

def trigger_led_flash(duration: float = 0.8):
    """Triggers a temporary LED flash (non-blocking)."""
    global led_flash_active, led_flash_end_time
    led_flash_end_time = time.time() + duration
    led_flash_active = True

def led_control_loop():
    """Background thread to manage LED states based on connection status and scan events."""
    global led_flash_active
    blink_state = False
    
    # Startup test: blink 3 times
    for _ in range(3):
        set_led_hardware(True)
        time.sleep(0.1)
        set_led_hardware(False)
        time.sleep(0.1)
        
    while True:
        try:
            current_time = time.time()
            if led_flash_active:
                if current_time < led_flash_end_time:
                    set_led_hardware(False)
                else:
                    set_led_hardware(True)
                    led_flash_active = False
                time.sleep(0.05)
                continue
                
            # Check for error conditions:
            # 1. PC connection error (not open / disconnected)
            # 2. PaSoRi error (state is connected but NFC worker is not ready)
            is_nfc_error = (state == STATE_CONNECTED and not nfc_ready.is_set())
            is_pc_error = (state == STATE_DISCONNECTED)
            
            if is_pc_error or is_nfc_error:
                # Slow blink on error (1.0s ON / 1.0s OFF)
                set_led_hardware(True)
                for _ in range(20):
                    if led_flash_active or state == STATE_CONNECTING or (state == STATE_CONNECTED and nfc_ready.is_set()):
                        break
                    time.sleep(0.05)
                if led_flash_active or state == STATE_CONNECTING or (state == STATE_CONNECTED and nfc_ready.is_set()):
                    continue
                    
                set_led_hardware(False)
                for _ in range(20):
                    if led_flash_active or state == STATE_CONNECTING or (state == STATE_CONNECTED and nfc_ready.is_set()):
                        break
                    time.sleep(0.05)
                continue
                
            # Manage LED in normal states
            if state == STATE_CONNECTING:
                # Double-blink pattern (Blink twice, then wait 0.6s)
                set_led_hardware(True)
                time.sleep(0.15)
                set_led_hardware(False)
                time.sleep(0.15)
                set_led_hardware(True)
                time.sleep(0.15)
                set_led_hardware(False)
                # Responsive pause for 0.6s
                for _ in range(12):
                    if led_flash_active or state != STATE_CONNECTING:
                        break
                    time.sleep(0.05)
            elif state == STATE_CONNECTED:
                set_led_hardware(True)
                time.sleep(0.2)
        except Exception as e:
            log(f"LED Control Thread: Error - {e}")
            time.sleep(1)

def set_servo_angle(angle: float):
    """Sets the servo motor angle on GPIO12 (0 to 90 degrees) using pigpio (jitter-free)."""
    if not has_servo or not pi:
        return
    try:
        # Map 0° to 90° -> 500us to 1500us
        pulse_width = 500 + (angle / 90.0) * 1000
        pulse_width = max(500, min(2500, pulse_width))
        pi.set_servo_pulsewidth(SERVO_PIN, int(pulse_width))
    except Exception as e:
        log(f"Servo: Error setting angle to {angle}° - {e}")

def servo_control_loop():
    """Background thread to manage servo position based on connection states."""
    last_state = None
    
    # Move to initial position on startup
    set_servo_angle(0)
    time.sleep(0.5)
    
    while True:
        try:
            if state == STATE_CONNECTING:
                # Rotate between 0 and 90 degrees repeatedly (1.0s at each position, halving the speed)
                set_servo_angle(0)
                for _ in range(20): # 20 * 0.05s = 1.0s
                    if state != STATE_CONNECTING:
                        break
                    time.sleep(0.05)
                if state != STATE_CONNECTING:
                    continue
                    
                set_servo_angle(90)
                for _ in range(20): # 20 * 0.05s = 1.0s
                    if state != STATE_CONNECTING:
                        break
                    time.sleep(0.05)
            elif state == STATE_CONNECTED:
                # Fix at 90 degrees
                if last_state != STATE_CONNECTED:
                    set_servo_angle(90)
                time.sleep(0.2)
            else:
                # Disconnected / initial state
                if last_state != STATE_DISCONNECTED:
                    set_servo_angle(0)
                time.sleep(0.2)
                
            last_state = state
        except Exception as e:
            log(f"Servo Control Thread: Error - {e}")
            time.sleep(1)

def shutdown_monitor_loop():
    """Background thread to monitor the physical shutdown button on BCM 26."""
    if not has_shutdown_btn:
        return
        
    press_duration = 0.0
    while True:
        try:
            is_pressed = False
            if gpio_mode == "rpi":
                # With internal pull-up, button press pulls pin to GND (LOW / False)
                is_pressed = (GPIO.input(SHUTDOWN_PIN) == GPIO.LOW)
            elif gpio_mode == "gpiozero" and shutdown_btn_device:
                is_pressed = shutdown_btn_device.is_pressed
                
            if is_pressed:
                press_duration += 0.1
                # Trigger shutdown if held for 1.5 seconds to avoid accidental presses
                if press_duration >= 1.5:
                    log("Shutdown Button: Triggering system shutdown...")
                    # 1. Blink LED rapidly to indicate shutdown starting
                    for _ in range(5):
                        set_led_hardware(True)
                        time.sleep(0.05)
                        set_led_hardware(False)
                        time.sleep(0.05)
                    # 2. Return servo to 0
                    set_servo_angle(0)
                    time.sleep(0.5)
                    # 3. Clean up and halt system
                    reset_connection()
                    os.system("sudo shutdown -h now")
                    sys.exit(0)
            else:
                press_duration = 0.0
                
            time.sleep(0.1)
        except Exception as e:
            log(f"Shutdown Monitor: Error - {e}")
            time.sleep(1)

def safe_serial_write(data: bytes):
    """Safely writes to serial and adds a delay to prevent chunk concatenation in the Windows app."""
    global serial_port
    with write_lock:
        if serial_port and serial_port.is_open:
            serial_port.write(data)
            serial_port.flush()
            time.sleep(0.2)  # Wait 200ms to guarantee the Windows app reads it as a separate chunk


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
        
        # Trigger LED flash feedback (0.8s)
        trigger_led_flash(0.8)
        
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
                        
                        # Transition immediately to allow serial communication while NFC initializes in background
                        state = STATE_CONNECTED
                        log("System: PC Handshake complete. Connected and operational (NFC initializing in background).")
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
    # Start LED background thread
    led_thread = threading.Thread(target=led_control_loop, daemon=True)
    led_thread.start()

    # Start Servo background thread
    servo_thread = threading.Thread(target=servo_control_loop, daemon=True)
    servo_thread.start()

    # Start Shutdown Monitor background thread
    shutdown_thread = threading.Thread(target=shutdown_monitor_loop, daemon=True)
    shutdown_thread.start()

    try:
        main_loop()
    except KeyboardInterrupt:
        log("System: Interrupted by user (KeyboardInterrupt).")
        reset_connection()
        sys.exit(0)
