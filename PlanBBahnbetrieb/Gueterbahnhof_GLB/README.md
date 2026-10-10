# Güterbahnhof – passendes GLB-Asset

06_freight_station.glb ist echte 3D-Geometrie im gleichen hellen, futuristischen Stil wie die fünf Gebäude. Enthalten sind Kontrollturm, Ladehalle mit vier Toren, Ladeplattform und Vordach, zwei Durchgangsgleise, Portalkran, Frachtcontainer und ein statischer Güterwagen.

43 echte Fassadenaussparungen für Fenster und Tore, mit Laibungen und innenliegenden Glas-/Torflächen. Fenster auf Vorderseite, Rückseite und beiden Außenflanken. Dachlüfter, Oberlichter, profilierte Schienen, Schwellen, Containerstreben und Wagenräder sind modelliert.

GLB: 7.355 Dreiecke, 25 nach Material und Funktion zusammengefasste Meshes, sechs PBR-Materialien; keine externen Texturen. Y ist oben, Ursprung mittig am Boden. Grundfläche 14 × 9 Modelleinheiten, Höhe etwa 5,1. Abmessungen sind gestalterische Spielmaße, keine maßstäbliche technische Bahnplanung.

Zwei gerade Gleise verlaufen entlang X. Schienenköpfe liegen bei Y=0,4125. Im GLB liegen die Gleismitten bei Z=2,05 und Z=3,63; Gleisenden bei X=-7 und X=7. Achsabstand der Schienen eines Gleises: 0,64. Gleisverbindung und Spiellogik implementiert Codex im Spiel.

Der Wagen ist statisch; Mesh-Namen beginnen mit wagon_. Er kann entfernt oder per gemeinsamem Group-Objekt bewegt werden. Die übrigen Gruppen heißen station_, tracks_, gantry_, cargo_ und roof_equipment_. Der Kran ist ebenfalls statisch und enthält keine Animation.

preview_front.png und preview_rear.png sind direkt aus der exportierten Modellgeometrie gerenderte Ansichten. Eine zusätzlich in der Unterhaltung erzeugte Illustration zeigt den gewünschten Präsentationsstil und kann vom GLB in Details abweichen.

Einbau mit Three.js:
const gltf = await new GLTFLoader().loadAsync('/assets/06_freight_station.glb');
scene.add(gltf.scene);

Reproduktion: Python mit NumPy, Pillow und Matplotlib; im Paketordner python build_station.py ausführen. geometry.py enthält die Geometriefunktionen. GLB-Längen und Buffergrenzen, endliche Koordinaten und die Wandöffnungen an jeweils neun Stellen sind geprüft.
