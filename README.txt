SPUŠTĚNÍ ELECTROVENTORY

Požadavek: Na počítači musí být nainstalované .NET 8 SDK.

_________________________________________________________________________________

1. -------- LOKÁLNÍ SPUŠTĚNÍ --------

1.1. Stažení repozitáře a přechod do hlavní složky projektu:
     git clone https://github.com/IJEN00/ElectroVentory.git
     cd ElectroVentory/InventoryApp

1.2. Obnovení závislostí a spuštění serveru:
     dotnet run

     (Poznámka: Databázové migrace a soubor SQLite databáze 
     se vytvoří zcela automaticky při prvním startu).

1.3. Otevření webového prohlížeče a přechod na adresu, kterou 
     vypsal terminál (typicky např. http://localhost:5139).

__________________________________________________________________________________

2. -------- NASAZENÍ NA RASPBERRY PI --------

2.1. Vygenerování produkčního balíčku pro OS Linux (architektura ARM64):
     dotnet publish -c Release -r linux-arm64 --self-contained true

2.2. Zkopírování souborů na Raspberry Pi:
     Zajistěte, že na Raspberry Pi existuje cílová složka (např. /home/pi/ElectroVentory).

     Z vašeho PC překopírujte všechny vygenerované soubory pomocí tohoto příkazu:    
     scp -r ./bin/Release/net8.0/linux-arm64/publish/* pi@<IP_ADRESA>:/home/pi/ElectroVentory

2.3. Připojení k Raspberry Pi a nastavení práv:     
     ssh pi@<IP_ADRESA>

     Přejděte do složky s aplikací a nastavte hlavnímu souboru právo ke spuštění:   
     cd /home/pi/ElectroVentory
     chmod +x InventoryApp

2.4. Vytvoření služby pro běh na pozadí (systemd):
     
     Otevřete textový editor:
     sudo nano /etc/systemd/system/electroventory.service

     Do editoru vložte následující konfiguraci:

     [Unit]
     Description=ElectroVentory Web App
     After=network.target

     [Service]
     WorkingDirectory=/home/pi/ElectroVentory
     ExecStart=/home/pi/ElectroVentory/InventoryApp
     Restart=always
     RestartSec=10
     SyslogIdentifier=electroventory
     User=pi
     Environment=ASPNETCORE_ENVIRONMENT=Production
     Environment=ASPNETCORE_URLS=http://0.0.0.0:5000

     [Install]
     WantedBy=multi-user.target

     Uložení souboru (Ctrl+O, Enter) a zavření editoru (Ctrl+X).

2.5. Aktivace a spuštění služby:    
     sudo systemctl daemon-reload
     sudo systemctl enable electroventory.service
     sudo systemctl start electroventory.service

2.6. Aplikace nyní trvale běží na pozadí. Můžete ji otevřít ve webovém 
     prohlížeči na jakémkoliv zařízení v síti na adrese: 
     http://<IP_ADRESA>:5000 
     
     Ověření stavu služby: 
     sudo systemctl status electroventory.service

__________________________________________________________________________________

3. -------- NASTAVENÍ API KLÍČŮ --------

Pro funkční vyhledávání cen u dodavatelů je nutné vložit platné API klíče:
- TME API (vyžaduje Token a AppSecret)
- Mouser API (vyžaduje Search API Key)

Pro lokální vývoj se používá nástroj Secret Manager, pro produkci se 
vkládají do appsettings.json nebo jako proměnné prostředí OS.
