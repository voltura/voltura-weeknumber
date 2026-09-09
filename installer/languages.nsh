; Keep IDs and native labels aligned with the application LanguageCatalog.
; NSIS supplies the standard wizard translations; Cantonese is maintained here.
!macro VolturaLanguage Id Nlf NativeName
  !define LANGFILE_LANGDLL_FMT "${NativeName}"
  !if "${Id}" == "yue"
    !insertmacro MUI_LANGUAGEEX "languages" "${Nlf}"
  !else
    !insertmacro MUI_LANGUAGE "${Nlf}"
  !endif
  !undef LANGFILE_LANGDLL_FMT
!macroend

!insertmacro VolturaLanguage "en" "English" "English"
!insertmacro VolturaLanguage "sv" "Swedish" "Svenska"
!insertmacro VolturaLanguage "de" "German" "Deutsch"
!insertmacro VolturaLanguage "fr" "French" "Français"
!insertmacro VolturaLanguage "da" "Danish" "Dansk"
!insertmacro VolturaLanguage "fi" "Finnish" "Suomi"
!insertmacro VolturaLanguage "is" "Icelandic" "Íslenska"
!insertmacro VolturaLanguage "nb" "Norwegian" "Norsk bokmål"
!insertmacro VolturaLanguage "pl" "Polish" "Polski"
!insertmacro VolturaLanguage "it" "Italian" "Italiano"
!insertmacro VolturaLanguage "es" "Spanish" "Español"
!insertmacro VolturaLanguage "yue" "Cantonese" "粵語"
!insertmacro VolturaLanguage "ja" "Japanese" "日本語"
!insertmacro VolturaLanguage "pt-BR" "PortugueseBR" "Português (Brasil)"
!insertmacro VolturaLanguage "zh-Hans" "SimpChinese" "简体中文"
!insertmacro VolturaLanguage "zh-Hant" "TradChinese" "繁體中文"
!insertmacro VolturaLanguage "nl" "Dutch" "Nederlands"
!insertmacro VolturaLanguage "ko" "Korean" "한국어"
!insertmacro VolturaLanguage "ru" "Russian" "Русский"
!insertmacro VolturaLanguage "tr" "Turkish" "Türkçe"
!insertmacro VolturaLanguage "id" "Indonesian" "Bahasa Indonesia"

; Application-owned setup and uninstall messages. Native Windows dialogs and
; the original license text retain their own language.
!macro VolturaSetupStrings Nlf Failure Runtime Removal Architecture
  LangString Failed ${LANG_${Nlf}} "${Failure}"
  LangString RuntimeFailed ${LANG_${Nlf}} "${Runtime}"
  LangString RemoveData ${LANG_${Nlf}} "${Removal}"
  LangString RequiresX64 ${LANG_${Nlf}} "${Architecture}"
!macroend

!insertmacro VolturaSetupStrings English \
  "Setup could not complete. The previous installation was restored where possible. See the installation details and retry." \
  "The .NET 10 Desktop Runtime could not be installed. Retry, or use the offline installer." \
  "Also remove your settings, logs, and downloaded updates?" \
  "Windows x64 is required."
!insertmacro VolturaSetupStrings Swedish \
  "Installationen kunde inte slutföras. Den tidigare installationen återställdes om möjligt. Se installationsinformationen och försök igen." \
  ".NET 10 Desktop Runtime kunde inte installeras. Försök igen eller använd offline-installationsprogrammet." \
  "Vill du också ta bort inställningar, loggar och hämtade uppdateringar?" \
  "Windows x64 krävs."
!insertmacro VolturaSetupStrings German \
  "Setup konnte nicht abgeschlossen werden. Die vorherige Installation wurde nach Möglichkeit wiederhergestellt. Details prüfen und erneut versuchen." \
  ".NET 10 Desktop Runtime konnte nicht installiert werden. Erneut versuchen oder Offline-Installer verwenden." \
  "Auch Einstellungen, Protokolle und heruntergeladene Updates entfernen?" \
  "Windows x64 ist erforderlich."
!insertmacro VolturaSetupStrings French \
  "L’installation n’a pas pu être terminée. L’installation précédente a été restaurée dans la mesure du possible. Consultez les détails de l’installation et réessayez." \
  "Le .NET 10 Desktop Runtime n’a pas pu être installé. Réessayez ou utilisez le programme d’installation hors ligne." \
  "Supprimer également vos paramètres, journaux et mises à jour téléchargées ?" \
  "Windows x64 est requis."
!insertmacro VolturaSetupStrings Danish \
  "Installationen kunne ikke fuldføres. Den tidligere installation blev gendannet, hvor det var muligt. Se installationsoplysningerne, og prøv igen." \
  ".NET 10 Desktop Runtime kunne ikke installeres. Prøv igen, eller brug offlineinstallationsprogrammet." \
  "Vil du også fjerne dine indstillinger, logfiler og hentede opdateringer?" \
  "Windows x64 er påkrævet."
!insertmacro VolturaSetupStrings Finnish \
  "Asennusta ei voitu viimeistellä. Aiempi asennus palautettiin mahdollisuuksien mukaan. Tarkista asennuksen tiedot ja yritä uudelleen." \
  ".NET 10 Desktop Runtime -ympäristöä ei voitu asentaa. Yritä uudelleen tai käytä offline-asennusohjelmaa." \
  "Poistetaanko myös asetukset, lokit ja ladatut päivitykset?" \
  "Windows x64 vaaditaan."
!insertmacro VolturaSetupStrings Icelandic \
  "Ekki tókst að ljúka uppsetningunni. Fyrri uppsetning var endurheimt þar sem hægt var. Skoðaðu upplýsingar um uppsetninguna og reyndu aftur." \
  "Ekki tókst að setja upp .NET 10 Desktop Runtime. Reyndu aftur eða notaðu uppsetningarforritið fyrir ótengda uppsetningu." \
  "Viltu einnig fjarlægja stillingar, annála og sóttar uppfærslur?" \
  "Windows x64 er nauðsynlegt."
!insertmacro VolturaSetupStrings Norwegian \
  "Installasjonen kunne ikke fullføres. Den forrige installasjonen ble gjenopprettet der det var mulig. Se installasjonsdetaljene og prøv igjen." \
  ".NET 10 Desktop Runtime kunne ikke installeres. Prøv igjen, eller bruk det frakoblede installasjonsprogrammet." \
  "Vil du også fjerne innstillinger, logger og nedlastede oppdateringer?" \
  "Windows x64 kreves."
!insertmacro VolturaSetupStrings Polish \
  "Nie udało się ukończyć instalacji. Poprzednia instalacja została przywrócona tam, gdzie było to możliwe. Sprawdź szczegóły instalacji i spróbuj ponownie." \
  "Nie udało się zainstalować środowiska .NET 10 Desktop Runtime. Spróbuj ponownie lub użyj instalatora offline." \
  "Czy usunąć również ustawienia, dzienniki i pobrane aktualizacje?" \
  "Wymagany jest system Windows x64."
!insertmacro VolturaSetupStrings Italian \
  "Impossibile completare l’installazione. L’installazione precedente è stata ripristinata ove possibile. Consulta i dettagli dell’installazione e riprova." \
  "Impossibile installare .NET 10 Desktop Runtime. Riprova o usa il programma di installazione offline." \
  "Rimuovere anche le impostazioni, i registri e gli aggiornamenti scaricati?" \
  "È richiesto Windows x64."
!insertmacro VolturaSetupStrings Spanish \
  "No se pudo completar la instalación. Se restauró la instalación anterior cuando fue posible. Consulta los detalles de la instalación e inténtalo de nuevo." \
  "No se pudo instalar .NET 10 Desktop Runtime. Inténtalo de nuevo o utiliza el instalador sin conexión." \
  "¿Eliminar también la configuración, los registros y las actualizaciones descargadas?" \
  "Se requiere Windows x64."
!insertmacro VolturaSetupStrings Cantonese \
  "安裝未能完成。已盡可能還原之前嘅安裝。請睇吓安裝詳情，再試一次。" \
  "裝唔到 .NET 10 Desktop Runtime。請再試一次，或者用離線安裝程式。" \
  "要唔要一併刪除你嘅設定、記錄同已下載嘅更新？" \
  "需要 Windows x64。"
!insertmacro VolturaSetupStrings Japanese \
  "インストールを完了できませんでした。可能な範囲で以前のインストールを復元しました。インストールの詳細を確認して、もう一度お試しください。" \
  ".NET 10 Desktop Runtime をインストールできませんでした。もう一度試すか、オフラインインストーラーを使用してください。" \
  "設定、ログ、ダウンロード済みの更新も削除しますか？" \
  "Windows x64 が必要です。"
!insertmacro VolturaSetupStrings PortugueseBR \
  "Não foi possível concluir a instalação. A instalação anterior foi restaurada quando possível. Confira os detalhes da instalação e tente novamente." \
  "Não foi possível instalar o .NET 10 Desktop Runtime. Tente novamente ou use o instalador offline." \
  "Remover também suas configurações, registros e atualizações baixadas?" \
  "É necessário o Windows x64."
!insertmacro VolturaSetupStrings SimpChinese \
  "无法完成安装。已尽可能恢复先前的安装。请查看安装详情并重试。" \
  "无法安装 .NET 10 Desktop Runtime。请重试，或使用离线安装程序。" \
  "是否同时删除设置、日志和已下载的更新？" \
  "需要 Windows x64。"
!insertmacro VolturaSetupStrings TradChinese \
  "無法完成安裝。已盡可能還原先前的安裝。請查看安裝詳細資料並重試。" \
  "無法安裝 .NET 10 Desktop Runtime。請重試，或使用離線安裝程式。" \
  "是否一併移除設定、記錄及已下載的更新？" \
  "需要 Windows x64。"
!insertmacro VolturaSetupStrings Dutch \
  "De installatie kon niet worden voltooid. De vorige installatie is waar mogelijk hersteld. Bekijk de installatiedetails en probeer het opnieuw." \
  ".NET 10 Desktop Runtime kon niet worden geïnstalleerd. Probeer het opnieuw of gebruik het offline-installatieprogramma." \
  "Ook uw instellingen, logboeken en gedownloade updates verwijderen?" \
  "Windows x64 is vereist."
!insertmacro VolturaSetupStrings Korean \
  "설치를 완료하지 못했습니다. 가능한 경우 이전 설치를 복원했습니다. 설치 세부 정보를 확인하고 다시 시도하세요." \
  ".NET 10 Desktop Runtime을 설치하지 못했습니다. 다시 시도하거나 오프라인 설치 프로그램을 사용하세요." \
  "설정, 로그 및 다운로드한 업데이트도 삭제하시겠습니까?" \
  "Windows x64가 필요합니다."
!insertmacro VolturaSetupStrings Russian \
  "Не удалось завершить установку. Предыдущая установка по возможности восстановлена. Просмотрите подробности установки и повторите попытку." \
  "Не удалось установить .NET 10 Desktop Runtime. Повторите попытку или используйте автономный установщик." \
  "Также удалить настройки, журналы и загруженные обновления?" \
  "Требуется Windows x64."
!insertmacro VolturaSetupStrings Turkish \
  "Kurulum tamamlanamadı. Önceki kurulum mümkün olduğunca geri yüklendi. Kurulum ayrıntılarını inceleyip yeniden deneyin." \
  ".NET 10 Desktop Runtime yüklenemedi. Yeniden deneyin veya çevrimdışı yükleyiciyi kullanın." \
  "Ayarlarınız, günlükleriniz ve indirilen güncellemeler de kaldırılsın mı?" \
  "Windows x64 gereklidir."
!insertmacro VolturaSetupStrings Indonesian \
  "Instalasi tidak dapat diselesaikan. Instalasi sebelumnya dipulihkan jika memungkinkan. Lihat detail instalasi dan coba lagi." \
  ".NET 10 Desktop Runtime tidak dapat diinstal. Coba lagi atau gunakan penginstal offline." \
  "Hapus juga pengaturan, log, dan pembaruan yang telah diunduh?" \
  "Memerlukan Windows x64."
