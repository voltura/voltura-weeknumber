# Installer artwork

Welcome and Finish share `installer/Assets/wizard.bmp`, replacing the NSIS stock computer illustration. The artwork has no language-specific text. NSIS uses `AspectFitHeight` to preserve its proportions at different display scales and with localized dialog dimensions.

The high-resolution original is `installer/Assets/wizard-master.png`. The source is converted without resizing to an opaque 24-bit RGB BMP. Regenerate the NSIS bitmap with:

```powershell
./scripts/build-installer-art.ps1
```

Packaging runs this conversion automatically.
