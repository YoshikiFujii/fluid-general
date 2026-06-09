#nfc-debug.py テスト用プログラム
#nfcの中身をすべてダンプするプログラム
import nfc

def on_connect(tag: nfc.tag.Tag) -> None:
    print("\n".join(tag.dump()))

with nfc.ContactlessFrontend("usb") as clf:
    clf.connect(rdwr={"on-connect": on_connect})