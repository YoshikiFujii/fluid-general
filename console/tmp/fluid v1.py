import serial
import time
import nfc
import sys
import asyncio
from typing import cast
from concurrent.futures import ThreadPoolExecutor

student_num_global = "000000000"
Serial_Port = None
shutdown_signal = 0

def connect():
    print('Connecting...')
    global Serial_Port,port_name,shutdown_signal
    while shutdown_signal==0:
        port_name="/dev/serial0"
        Serial_Port=serial.Serial(port=port_name, baudrate=115200, parity='N') #portオープン
        data=Serial_Port.readline(8).strip()# 1byte受信なら data=Serial_Port.read(1)
        data=data.decode('utf-8')

        if data == "cntfluid":
            AnswerCode = "hithere!"
            AnswerCode = AnswerCode.encode('utf-8')
            print('Connected to'+port_name)
            Serial_Port.write(AnswerCode)
            return
        else:
            print("fault:TextIsNotCollect")
            print(data)
        time.sleep(1)

def on_connect(tag):
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
    
    except Exception as e:
        print(f"NFC error: {e}")
        student_num_global = "000000000"

def write_code(code):
    try:
        Serial_Port.write(code.encode('utf-8'))
        print(f"Sent code: {code}")
    except serial.SerialException as e:
        print(f"Serial error: {e}")
    except Exception as e:
        print(f"Error: {e}")

async def shutdown():
    global shutdown_signal
    shutdown_signal=1
    print("Shutting down...")
    if Serial_Port and Serial_Port.is_open:
        Serial_Port.close()  # シリアルポートを閉じる
    # イベントループを停止してプログラムを終了
    tasks = [t for t in asyncio.all_tasks() if t is not asyncio.current_task()]
    for task in tasks:
        task.cancel()
    # キャンセルが反映されるまで少し待つ
    await asyncio.sleep(0)
    try:
        await asyncio.gather(*tasks, return_exceptions=True)
    except asyncio.CancelledError:
        pass
    print("All tasks cancelled.")
    # イベントループを停止
    loop = asyncio.get_running_loop()
    loop.stop()
    print("Event loop stopped.")
    sys.exit()

async def read_command():
    global Serial_Port,shutdown_signal
    while shutdown_signal==0:
        try:
            if Serial_Port.in_waiting > 0:
                # シリアル信号が来た場合
                data = Serial_Port.readline(8).strip()
                if(data.decode('utf-8') == "22222222"):
                    print("Command:finish")
                    await shutdown()
                    return
                if(data.decode('utf-8') == "11111111"):
                    print("Command:restart")
                    await shutdown()
                    await main()
                    return
            await asyncio.sleep(0.1)  # 少し待機
        except Exception as e:
            print(f"Serial read error: {e}")
            break
    
# NFCタグを待つ関数を別スレッドで実行
async def wait_for_nfc(clf):
    global student_num_global,shutdown_signal
    loop = asyncio.get_event_loop()
    with ThreadPoolExecutor() as pool:
        while shutdown_signal==0:
            try:
                await loop.run_in_executor(pool, lambda: clf.connect(rdwr={'on-connect': on_connect}))
                if student_num_global != "000000000":
                    print("scanned code:" + student_num_global)
                    write_code(student_num_global)
                    student_num_global = "000000000"
                else:
                    print("not student ID card")
                    write_code(student_num_global)
                await asyncio.sleep(0.7)  # 停止
            except nfc.ContactlessFrontendError as e:
                print(f"NFC error: {e}")
                break

async def main():
    global student_num_global,shutdown_signal
    # シリアル接続されるまで待機
    shutdown_signal=0
    connect()

    # NFCの接続をループで待つ
    clf = nfc.ContactlessFrontend("usb")
    #バックグラウンドでPCからのコマンドをまつ
    asyncio.create_task(read_command())

    try:
        # NFCタグ待ちを別スレッドで実行
        await wait_for_nfc(clf)
    finally:
        clf.close()

if __name__ == '__main__':
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        print("Program interrupted and terminated.")