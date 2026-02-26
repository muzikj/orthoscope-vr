# OrthoScope VR

**OrthoScope VR** je modulární VR platforma pro ortodontickou diagnostiku a vizualizaci dentálních dat. Projekt je vyvíjen v Unity pro headset Meta Quest a kombinuje tři hlavní moduly:

1. **ScanModule** – 3D modely zubních oblouků (STL)  
2. **PhotoModule** – automatizovaná postprodukce a analýza 2D fotografií ústní dutiny (JPG, PNG)  
3. **CephModule** – vizualizace rentgenových snímků a kefalometrická analýza (CSV export z OnyxCeph)

V současnosti jsou jednotlivé moduly vyvíjeny jako **samostatné Unity projekty**, ale repozitář je připraven pro jejich budoucí sloučení do **jedné integrované platformy**.

---

## 📂 Struktura repozitáře


orthoscope-vr/
│
├── README.md
├── LICENSE
├── .gitignore
├── Docs/ # Dokumentace, UML, návrhy UI
│
├── ScanModule/ # Samostatný modul 3D modelů
│ ├── Assets/
│ ├── Packages/
│ └── ProjectSettings/
│
├── PhotoModule/ # Samostatný modul fotografií
│ ├── Assets/
│ ├── Packages/
│ └── ProjectSettings/
│
├── CephModule/ # Samostatný modul RTG/kefalometrie
│ ├── Assets/
│ ├── Packages/
│ └── ProjectSettings/
│
└── ExternalLibraries/ # Sdílené pluginy (např. Oculus Integration)


---

## ⚙️ Instalace a spuštění

1. **Unity** – doporučeno verze 2021 LTS nebo novější  
2. **Meta Quest Integration** – nainstalovat Oculus Integration nebo OpenXR plugin  
3. **Otevřít modul** – otevřít složku `ScanModule`, `PhotoModule` nebo `CephModule` jako samostatný Unity projekt  
4. **Build & Run** – exportovat APK pro Meta Quest přes `File > Build Settings > Android`

> ⚠️ Upozornění: Repoxitář obsahuje **ukázková data** pro testování. Reálná data pacientů nesmí být verzována.

---

## 🧩 Plánovaná integrace

Po dokončení vývoje všech modulů bude:

- **Sloučení do jednoho Unity projektu**  
- Modulární struktura v `Assets/Scripts/` pro Scan / Photo / Ceph  
- Sdílené UI, ovládání VR a export dat  
- Možnost budoucího rozšíření o diagnostické nástroje a měření

---

## 📖 Dokumentace

- `Docs/architecture.md` – architektura softwaru  
- `Docs/user_manual.md` – manuál pro testování modulů ve VR  
- `Docs/glossary.md` – terminologie (Ortho, Cephalo, Dent, VR, XR)  

---

## 📄 Licence

Projekt je licencován pod **MIT License**. Viz soubor `LICENSE`.
