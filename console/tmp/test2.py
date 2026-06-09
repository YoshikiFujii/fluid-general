import serial
import nfc
import time
import threading
import sys
from typing import cast

# グローバル変数
student_num_global = ""
Serial_Port = None
port_name = "/dev/serial0"
shutdown_signal = False

def connect():
    """シリアルポートに接続し、初期メッセージを確認する関数"""
    global Serial_Port, port_name
    print('Connecting...')

    try:
        # シリアルポートに接続
        Serial_Port = serial.Serial(port=port_name, baudrate=115200, parity=serial.PARITY_NONE)
        print(f"ポート {port_name} からの接続待ち...")

        while True:
            # データを受信
            data = Serial_Port.readline(8).strip().decode('utf-8')

            if data == "cntfluid":
                answer_code = "hithere!".encode('utf-8')
                Serial_Port.write(answer_code)
                print(f"{port_name} に接続が確認されました")
                # NFCスキャナに接続
                nfc_connect_and_read()
                # ポート接続完了後、コマンド監視開始
                monitor_commands()
                break
            else:
                print("fault: Text is not correct")
                print(f"受信データ: {data}")

            time.sleep(1)
    except Exception as e:
        print(f"シリアルポートエラー: {e}")
        time.sleep(1)  # エラー発生時に再試行のための待機

def nfc_connect_and_read():
    """NFCスキャナに接続し、IDを読み取る関数"""
    global Serial_Port,shutdown_signal
    try:
        clf = nfc.ContactlessFrontend("usb")
        print("NFCスキャナに接続しました")
        while not shutdown_signal:
            # NFCカードがスキャンされたときに実行
            clf.connect(rdwr={'on-connect': on_connect}, terminate=lambda: shutdown_signal, timeout=1.0)
            if student_num_global and not shutdown_signal:
                # 読み取ったIDをシリアルポートに送信
                Serial_Port.write(student_num_global.encode('utf-8'))
                print(f"学生番号 {student_num_global} をシリアル通信で送信しました")
    except Exception as e:
        print(f"NFCエラー: {e}")
    finally:
        clf.close()

def monitor_commands():
    """シリアルポートからのコマンドを常に監視する関数"""
    global Serial_Port,shutdown_signal
    while True:
        data = Serial_Port.readline(8).strip().decode('utf-8')
        print(f"受信データ: {data}")

        if data == "11111111":
            shutdown_signal=True
            print("再接続")
            Serial_Port.close()
            time.sleep(1)
            shutdown_signal=False
            connect()  # 再接続
        elif data == "22222222":
            shutdown_signal=True
            print("プログラムを終了します")
            Serial_Port.close()
            sys.exit()

def on_connect(tag):
    """NFCタグから学生番号を読み取る関数"""
    global student_num_global
    sys_code = 0x81E1
    service_code = 0x300B
    try:
        idm, pmm = tag.polling(system_code=sys_code)
        tag.idm, tag.pmm, tag.sys = idm, pmm, sys_code
        sc = nfc.tag.tt3.ServiceCode(service_code >> 6, service_code & 0x3F)

        # 学生番号の読み込み
        bc = nfc.tag.tt3.BlockCode(0, service=0)
        student_num = cast(bytearray, tag.read_without_encryption([sc], [bc]))
        student_num = student_num.rstrip(b'\x00').decode("shift_jis")
        student_num_global = student_num
        print(f"学生番号: {student_num_global} ")
    except Exception as e:
        print(f"NFCエラー: {e}")
        student_num_global = "000000000"
    return True  # 接続完了を示す

# シリアルポート接続関数をスレッドで実行
serial_thread = threading.Thread(target=connect)
serial_thread.start()
