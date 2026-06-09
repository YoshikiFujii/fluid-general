import serial
import time
import nfc
from typing import cast

def connect():
    while(1):
        port_name="/dev/serial0"
        Serial_Port=serial.Serial(port=port_name, baudrate=115200, parity='N') #portオープン
        data=Serial_Port.readline(8) # 1byte受信なら data=Serial_Port.read(1)
        data=data.strip()
        data=data.decode('utf-8')

        if data == "cntfluid":
            AnswerCode = "hithere!"
            AnswerCode = AnswerCode.encode('utf-8')
            Serial_Port.write(AnswerCode)
            return
        else:
            print("fault:TextIsNotCollect")
            print(data)
            time.sleep(1)

def on_connect(tag):
    sys_code = 0x81E1
    service_code = 0x300B
    idm, pmm = tag.polling(system_code=sys_code)
    tag.idm, tag.pmm, tag.sys = idm, pmm, sys_code
    sc = nfc.tag.tt3.ServiceCode(service_code >> 6, service_code & 0x3F)

    # student_num
    bc = nfc.tag.tt3.BlockCode(0, service=0)
    student_num = cast(bytearray, tag.read_without_encryption([sc], [bc]))
    student_num = student_num.decode("shift_jis")
    print(str(student_num))

    return student_num

def write_code(code):
    port_name="/dev/serial0"
    Serial_Port=serial.Serial(port=port_name, baudrate=115200, parity='N') #portオープン
    AnswerCode = code
    AnswerCode = AnswerCode.encode('utf-8')
    Serial_Port.write(AnswerCode)

if __name__ == '__main__':
    # 接続されるまでループ
    connect()
    # nfc
    while True:
        clf = nfc.ContactlessFrontend("usb")
        try:
            code = clf.connect(rdwr={'on-connect': on_connect})    #nfcが現れるまで待機
            write_code(code)
            time.sleep(1)   #1秒停止
        finally:
            clf.close()