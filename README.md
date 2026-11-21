<p align="center">
  <h1 align="center">bdstudio.hu/ocrtranslate</h1>
  <p align="center"><strong>Fejlett, valós idejű képernyőfordító és OCR szoftver Windowsra</strong></p>
  <p align="center"><em>Advanced Real-Time Screen Translator & OCR Software for Windows</em></p>
</p>

<p align="center">
  <a href="#magyar">🇭🇺 Magyar leírás</a> •
  <a href="#english">🇬🇧 English Description</a> •
  <a href="https://github.com/davidbodri/ocrtranslate/releases/latest">🚀 Letöltés / Download</a>
</p>

---

<a name="magyar"></a>
# 🇭🇺 Magyar

A **bdstudio.hu/ocrtranslate** egy valós idejű képernyőfordító alkalmazás, amelyet kifejezetten játékokhoz, videókhoz és tetszőleges asztali alkalmazásokhoz fejlesztettek. A program a képernyő kijelölt részén lévő szövegeket mesterséges intelligenciával azonnal felismeri és tetszőleges nyelvre (pl. magyarra) fordítja, miközben a felirat közvetlenül a képernyőn lebegve jelenik meg.

### 🌟 Főbb funkciók

- **Valós idejű feliratozás:** Minimális késleltetéssel közvetlenül a tartalom felett jeleníti meg a magyar fordítást (az idegen nyelvű felirat bevillanása nélkül).
- **Több mesterséges intelligencia OCR motor:**
  - **Windows OCR:** Rendkívül gyors és alacsony erőforrás-igényű.
  - **EasyOCR:** Mélytanulásos (Deep Learning) neurális háló szokatlan betűtípusokhoz és játékbeli hátterekhez.
  - **ML Minőségpontozó:** Egyszerre több felismerő futhat párhuzamosan, egy gépi tanulási modell automatikusan a legpontosabb szövegváltozatot választja ki.
- **Fordító motorok:**
  - **Google Translate:** Kulcs nélkül, azonnal működik a dobozból kibontva.
  - **DeepL:** Hivatalos ingyenes és pro API kulcs támogatása (rendkívül pontos minőség).
  - **Yandex Translate:** Hivatalos API kulcs támogatása.
- **Letisztult, minimalista dizájn:** Modern, elegáns, zavaró vizuális effektusoktól mentes fekete-fehér kezelőfelület.
- **Teljesen hordozható és önálló (All-in-One):** A program beágyazva tartalmazza a .NET 8 futtatókörnyezetet és a Python motort, semmit nem kell külön feltelepíteni.
- **Automatikus frissítés:** Indításkor jelzi, ha a GitHubon új verzió jelenik meg.

---

### 📥 Letöltés és Telepítés

1. Töltsd le a legfrissebb telepítőt a [Releases oldalról](https://github.com/davidbodri/ocrtranslate/releases/latest):  
   👉 **`ocrtranslate-setup.exe`**
2. Futtasd a telepítőt, és kövesd a magyar nyelvű varázslót.
3. A telepítő kiteszi a parancsikont az Asztalra és a Start menübe.

---

### 🎮 Használat és Gyorsbillentyűk

| Gyorsbillentyű | Funkció |
| :--- | :--- |
| **`Alt + G`** | **Beállítások megnyitása** (Nyelvek, OCR motorok, kinézet kiválasztása) |
| **`Alt + Q`** | **Képernyőterület kijelölése** (Jelöld ki a fordítandó játék/videó feliratsávját) |
| **`~` (Tilde)** | **Fordítás indítása / leállítása** |
| **`Alt + T`** | **Fordító ablak elrejtése / megjelenítése** |

> **Tipp játékokhoz:** A feliratablak helyes megjelenítéséhez a játék beállításaiban javasolt a **Keret nélküli ablakos (Borderless Windowed)** vagy **Ablakos (Windowed)** módot használni a Teljes képernyő (Exclusive Fullscreen) helyett.

---

### 💻 Rendszerkövetelmények

- **Operációs rendszer:** Windows 10 (verzió: 2004 / build 19041 vagy újabb) vagy Windows 11 (64-bit)
- **Hardver:** DirectX 11 kompatibilis videokártya, min. 2 GB RAM (EasyOCR használata esetén min. 8 GB RAM és NVIDIA GPU ajánlott)

---

<br>

<a name="english"></a>
# 🇬🇧 English

**bdstudio.hu/ocrtranslate** is an advanced real-time screen translator designed for PC games, videos, and desktop applications. It extracts text from any defined area on your screen using state-of-the-art OCR engines and provides instant, seamless translation overlay with zero latency and no distracting original text flashing.

### 🌟 Key Features

- **Real-Time Subtitle Overlay:** Instant floating subtitle window displaying translated text directly above the game or video without flickering.
- **Multiple AI OCR Engines:**
  - **Windows OCR:** Ultra-fast, lightweight, built right into Windows.
  - **EasyOCR:** Deep learning neural network specialized in custom gamer fonts and noisy backgrounds.
  - **ML Scoring:** Automatically compares multiple OCR outputs and chooses the highest-confidence result.
- **Translation Services:**
  - **Google Translate:** Works out of the box with zero configuration.
  - **DeepL:** Official free/pro API key integration for top-tier translation accuracy.
  - **Yandex Translate:** Official API support.
- **Clean Minimalist Design:** High-contrast, distraction-free monochrome black & white user interface.
- **Completely Self-Contained:** Bundles embedded .NET 8 runtime and isolated portable Python environment—no prerequisites required.
- **Auto Update Notifications:** Automatically checks GitHub Releases for new updates on startup.

---

### 📥 Download & Installation

1. Download the latest installer from [Releases](https://github.com/davidbodri/ocrtranslate/releases/latest):  
   👉 **`ocrtranslate-setup.exe`**
2. Run the installer and follow the setup wizard.
3. Launch `ocrtranslate` directly from your Desktop or Start Menu.

---

### 🎮 Usage & Default Hotkeys

| Hotkey | Action |
| :--- | :--- |
| **`Alt + G`** | **Open Settings** (Configure languages, OCR engines, hotkeys, appearance) |
| **`Alt + Q`** | **Select Capture Area** (Draw a rectangle around the subtitle area) |
| **`~` (Tilde)** | **Start / Stop Translation** |
| **`Alt + T`** | **Show / Hide Subtitle Overlay** |

> **Pro-Tip for Gaming:** For seamless overlay display over games, run your games in **Borderless Windowed** or **Windowed** mode instead of Exclusive Fullscreen.

---

### 💻 System Requirements

- **OS:** Windows 10 (Version 2004 / Build 19041 or later) or Windows 11 (64-bit)
- **Hardware:** DirectX 11 compatible GPU, 2 GB RAM (8 GB RAM and NVIDIA GPU recommended for EasyOCR)

---

### 🛠️ Building from Source

*Requires Visual Studio 2022 and .NET 8 SDK.*

```bash
git clone https://github.com/davidbodri/ocrtranslate.git
cd ocrtranslate
dotnet build Translumo.sln -c Release
```

To compile the setup installer, open `installer.iss` in **Inno Setup 7** and click **Compile**.

---

### 📄 License & Credits

- Based on the original open-source project by Aleksei Vekhov ([Translumo](https://github.com/ramjke/Translumo)).
- Powered by [OpenCvSharp](https://github.com/shimat/opencvsharp), [EasyOCR](https://github.com/JaidedAI/EasyOCR), and [Material Design In XAML](https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit).
- Licensed under the **Apache License 2.0**.
