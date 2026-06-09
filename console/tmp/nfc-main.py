import nfc
from typing import cast
import time

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

br=0
while True:
    clf = nfc.ContactlessFrontend("usb")
    try:
        clf.connect(rdwr={'on-connect': on_connect})    #nfcが現れるまで待機
        time.sleep(1)   #1秒停止
    finally:
        clf.close()
    
    br = br+1
    if br==10:
        break