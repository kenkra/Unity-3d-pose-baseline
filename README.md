# Unity-3d-pose-baseline

[miu200521358/3d-pose-baseline-vmd](https://github.com/miu200521358/3d-pose-baseline-vmd)で自動トレースした3Dポーズ情報を使用して、Unityでキャラクターを動かすためのプログラムです。

内容は[Qiita記事](https://qiita.com/kenkra/items/7b5634ff7f8c6bf0257a)を参照ください。

## 使用方法
### 1. 3Dポーズデータの作成
以下の@miu200521358様の記事の参考にして動画から3Dポーズデータ(pos.txt)を作成してください。

クラウド(colab)でMMD自動トレース
https://qiita.com/miu200521358/items/fb0a7bcf2764d7797e26

### 2. キャラクターを動かす
上記で作成されるpos.txt、PosTxtReader.cs、BVHRecorder.csを適当なフォルダに配置し、PosTxtReader.csをキャラクターにアタッチ後、Pos Filenameにpos.txtのパスを指定し、再生してください。

![md_main](https://user-images.githubusercontent.com/23007499/80491926-51e4ab00-899e-11ea-9ce1-527903d67839.png)

### appx. モーションをBVH形式で保存する
Save Motionをチェックし、保存先をSave BVH Filenameに指定し(例:D:\pose\motion1.bvh)、再生してください。再生が終わるとファイルが出力されます。bvh形式のファイルはBlenderなどのアプリケーションで読み込むことができます。
