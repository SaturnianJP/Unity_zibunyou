# Unity_zibunyou
自分用に作成したエディタ拡張たちの集まりです。

## BlendshapeBaker
ブレンドシェイプの情報をメッシュにベイクする機能です。
VRM0.xでVRMを作成した際に、ウェイトが0ではないブレンドシェイプがあると、リップシンクの挙動が怪しかったので作りました。

加算して処理しているので、ボタンを押す度に 頂点/法線/タンジェント の位置が加算されていきます。
SkinnedMeshRendererがついたゲームオブジェクトをHierarchy上で右クリックすることで使用できます。