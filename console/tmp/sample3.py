import nfc
from typing import cast
import requests

def on_startup(targets):
    for target in targets:
        target.sensf_req=bytearray.fromhex("0000030000")
    return targets

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
    
if __name__=='__main__':
    while True:
        clf = nfc.ContactlessFrontend("usb")
        try:
            clf.connect(rdwr={'targets':[81E1],'on-startup':on_startup,'on-connect': on_connect})
        finally:
            clf.close()