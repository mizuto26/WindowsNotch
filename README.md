# WindowsNotch

Windows 上で mac 風のノッチ UI を再現する WPF アプリです。  
画面上部に常駐するノッチから、ファイルの一時保管と iCloud Drive への共有を行えます。

## Features

- mac ライクなノッチ UI
- ホバーで開閉する上部ノッチ
- ファイルの一時保管 Shelf
- iCloud Drive 共有フォルダへの送信
- 設定画面と iPhone 共有手順ガイド

## Tech Stack

- .NET 8
- WPF
- Windows 10/11

## Project Structure

```text
src/WindowsNotch.App
├─ App.xaml
├─ App.xaml.cs
├─ WindowsNotch.App.csproj
├─ Models
│  ├─ AppSettings.cs
│  └─ ShelfItem.cs
├─ Services
│  ├─ AppSettingsService.cs
│  ├─ CopyResult.cs
│  ├─ FileDropService.cs
│  ├─ ICloudDriveLocator.cs
│  ├─ ShelfService.cs
│  ├─ StartupRegistrationService.cs
│  └─ StorageCopyHelper.cs
└─ Views
   ├─ MainWindow.xaml
   ├─ MainWindow.xaml.cs
   ├─ MainWindow.Animation.cs
   ├─ MainWindow.DragAndShelf.cs
   ├─ MainWindow.Interop.cs
   ├─ MainWindow.SettingsOverlay.cs
   ├─ SettingsWindow.xaml
   ├─ SettingsWindow.xaml.cs
   ├─ ShareGuideWindow.xaml
   └─ ShareGuideWindow.xaml.cs
```

### Requirements

- .NET 8 SDK
- Windows

### Build

```powershell
dotnet build .\WindowsNotch.sln
```

### Run

```powershell
dotnet run --project .\src\WindowsNotch.App\WindowsNotch.App.csproj
```

## Sharing to iPhone

現在の共有方法はネイティブ AirDrop ではなく、`iCloud Drive\WindowsNotch` フォルダ経由です。

1. Windows に iCloud for Windows をインストールする
2. iCloud Drive を有効にする
3. Windows と iPhone で同じ Apple Account にサインインする
4. ノッチの `iCloud Drive` 側にファイルをドロップする
5. iPhone の Files アプリで `iCloud Drive > WindowsNotch` を開く

## License

This project is licensed under the MIT License. See [LICENSE](./LICENSE).
