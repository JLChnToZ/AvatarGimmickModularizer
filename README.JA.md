# アバターギミックモジュラー化ツール

このツールは、元々「破壊的」なインストールスクリプトを別のアバター上で「非破壊的/モジュラー化」された方法でパッケージ化することにより、アバターアクセサリー/ギミックのインストールをモジュラー化するためのものです。

このツールは、アバターにコンテンツを追加するインストーラスクリプトのみをサポートしています。スクリプトがアバターからコンテンツを破壊的に削除する場合、何をすべきかはわかりません。

また、ダミーアバターの他のすべてが元のアバターと同じであることを確認してください。そうしないと、モジュラー化が正しく機能しない可能性があります。

これは [Modular Avatar](https://modular-avatar.nadena.dev/) に依存しており、動作します。

## インストール方法

0. まだ [Modular Avatar](https://modular-avatar.nadena.dev/) をインストールしていない場合は、これらのパッケージリスト ([`https://vpm.nadena.dev/vpm.json`](vcc://vpm/addRepo?url=https://vpm.nadena.dev/vpm.json)) をお使いの [VCC](https://vcc.docs.vrchat.com/)、[ALCOM](https://vrc-get.anatawa12.com/alcom/) や [vrc-get](https://github.com/vrc-get/vrc-get) などに追加してください。  
1. まだ私のパッケージをインストールしていない場合は、[私のパッケージリスト](https://xtlcdn.github.io/vpm/) ([`https://xtlcdn.github.io/vpm/index.json`](vcc://vpm/addRepo?url=https://xtlcdn.github.io/vpm/index.json)) を追加してください。  
2. ご自身のアバタープロジェクトに **Avatar Gimmick Modularizer** パッケージを追加してください。（既にアバタープロジェクトを作成している前提です。）

## 使用法

0. 「Tools > JLChnToZ > Gimmick Modularizer」を開く
1. 作業するアバターをドロップし、「クローン」をクリックします。
2. クローンしたダミーアバターに必要なアクセサリーやギミックをインストールします。
3. インストール後、「モジュラー化」をクリックして、変更を元のアバターにパッケージ化します。
4. ギミックの再インストールや変更を行わない場合は、ダミーアバターを安全に削除できます。

## ライセンス

[MIT](LICENSE)