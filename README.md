# 環境
* Unity 2017.2.0f3
* Visual Studio 2017 (15.3.3)
* MixedRealityToolkit-Unity for Unity 2017.1.2
* Windows 10 build 1709 (Fall Creaters Update)
* Xbox Controller (Immersive Headsetで動かす場合)
* ウェブカメラ (Immersive Headsetで動かす場合)

# 概要
WebRTCを使ってHoloLens - UWPアプリ間、Immersive HeadSet - UWPアプリ間、HoloLens - Immersive HeadSet間でビデオチャットを行うサンプルです。  
本サンプルはRitchie Lozada氏が公開している[UnityWithWebRTC](https://github.com/ritchielozada/UnityWithWebRTC)をリファクタしたものです。  

# Unityアプリの設定
1. WebRtcSampleUnityAppを開きます
2. Mainシーン上、「WebRtcManager」の「Signalling_host」をシグナリングサーバ(後述)を動かすマシンのIPを、「Signalling_port」を「8888」に設定します
3. ビルドしてHoloLensかローカルマシン上にビルドします

# 使い方
1. ./SignallingServer/peerconnection_server.exe(シグナリングサーバ)を起動します
2. 別マシンでUWPアプリ(PeerCCなど)を起動してシグナリングサーバに接続します
3. Unityアプリを起動します
4. 「Connect Server」と書かれているCubeをAirTap(for HoloLens)、もしくはXBoxコントローラのAボタン(for Immersive Headset)でクリックします
5. peerconnection_server.exeのコンソール上で、接続ユーザ数が「2」であることを確認し、Unityアプリ上で「Connect Peer」をクリックします

# ディレクトリ構成

| ディレクトリ | 説明 |
|:-----------|:------------|
|SignallingServer|シグナリングサーバのexeファイルが入っている。|
|WebRtcPluginSample.UnityEditor|UnityEditor用のDLLプロジェクト。プラグインのスタブ。|
|WebRtcPluginSample.WSA|UWP用のDLLプロジェクト。プラグインの本体。|
|WebRtcPluginSample|プラグインのコードを管理する共有プロジェクト。上二つのプロジェクトから参照される。|
|WebRtcPluginSampleTest.WSA|プラグインの単体テストプロジェクト。|
|WebRtcSampleUnityApp|Unityアプリのプロジェクト。|

# 今後
* データチャネルを使ったサンプルを作る
* シグナリングサーバの独自実装
* MixedRealityCapture対応

# PeerCC
以下をクローン&ビルドしてください  
[https://github.com/webrtc-uwp/PeerCC-Sample](https://github.com/webrtc-uwp/PeerCC-Sample)

# UnityWithWebRTC
以下のリポジトリを参考にしています  
[https://github.com/ritchielozada/UnityWithWebRTC](https://github.com/ritchielozada/UnityWithWebRTC)