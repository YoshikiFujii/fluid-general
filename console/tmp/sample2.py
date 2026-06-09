import binascii
import nfc
import requests

class MyCardReader(object):
    # iPhoneのエクスプレスカードにアクセスするための処理
    def on_startup(self, targets):
        for target in targets:
            target.sensf_req=bytearray.fromhex("0000030000")
        return targets

    def on_connect(self, tag):
        #タッチ時の処理
        print("--- Touched")

        #タグ情報を全て表示
        print(tag)

        #IDmのみ取得して表示
        self.idm = binascii.hexlify(tag._nfcid)
        print("IDm: {idm}", str(self.idm))

        return True

    def read_id(self):
        clf = nfc.ContactlessFrontend('usb')
        try:
            # 交通系IDまたはモバイルSuicaのみに限定
            clf.connect(rdwr={'targets':['212F'], 'on-startup': self.on_startup, 'on-connect': self.on_connect})
        finally:
            clf.close()
            
if __name__ == '__main__':
    cr = MyCardReader()
    while True:
        print("--- Please Touch")
        cr.read_id()
        print("--- Released")
