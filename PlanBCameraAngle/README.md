# Plan B: Terraform – Camera Angle Mod

Mittlere Maustaste halten und ziehen: horizontal drehen, vertikal neigen.
Home/Pos1 setzt die Ansicht zurück. Das Mausrad zoomt weiterhin.
WASD und die Pfeiltasten bewegen die Ansicht relativ zur Kameradrehung:
W nach oben im Bild, S nach unten, A nach links und D nach rechts.
Rechte Maustaste und Ziehen verschieben die Ansicht ebenfalls relativ zur
Kameradrehung. Die native Anpassung der Ziehgeschwindigkeit berücksichtigt die Neigung.
Die Neigung ist auf 5–80 Grad gegenüber der senkrechten Ansicht begrenzt.
An der Neigungsgrenze reagiert Ziehen in Gegenrichtung sofort; überschüssige
Mausbewegung wird nicht gespeichert. Das gilt auch nach einem Zoomwechsel.
Die zusätzlichen Drehungen werden gemeinsam berechnet und mit einem Aufruf
auf Position und Ausrichtung der Kamera angewendet.
Menüs, Filmsequenzen und Tiersteuerung verwenden ihre ursprüngliche Kamera.
Beim Laden wird die Drehung zurückgesetzt; sie wird nicht im Spielstand gespeichert.

Die native Funktion „Auswählen und Kopieren“ bleibt auf der mittleren Maustaste aktiv.
Ein Mittelklick auf ein Gebäude wählt dessen Bauvorlage. Halten und Ziehen dreht
weiterhin die Kamera; beim Start auf einem Gebäude dürfen beide Aktionen erfolgen.
Empfindlichkeit: BepInEx/config/tony.planb.cameraangle.cfg (nach erstem Start).

Build: `dotnet build -c Release`. BepInEx 5 wird benötigt.
Installation: PlanBCameraAngle.dll in BepInEx/plugins/PlanBCameraAngle kopieren.
Gegen die lokal installierten Spielassemblies kompiliert; Bedienung, Sichtbarkeit
von Gebäuden und Bauauswahl müssen im laufenden Spiel geprüft werden.
