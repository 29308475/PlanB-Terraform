# Fünf Gebäude nach den gelieferten Plan-B-Referenzen

Echte GLB-Dateien mit eigener Geometrie, benannten Bauteilen und sechs PBR-Materialien. Keine Bildtafeln. Y ist oben; Boden liegt bei Y=0; Ursprung mittig unter dem Gebäude. Maße sind angenäherte Meter, da die Referenzen keinen Maßstab liefern.

01_stepped_tower.glb – asymmetrischer hoher Turm, Fensterbänder und vertikaler Fassadenkanal.
02_three_module_complex.glb – drei gestaffelte Module, Dachaufbauten, Fensterkanäle und Eingang.
03_buttressed_reactor.glb – seitliche Stützen, blaues Füllwerk, gelber Mittelschacht und geteilte Krone.
04_antenna_spire.glb – gespreizte Stützen, Fensterbänder und Dachantenne.
05_geodesic_dome.glb – einzelne dreieckige Paneele und dreidimensionales Strebenraster.

Die kleinen Referenzbilder erlauben eine Annäherung der sichtbaren Architektur, keine exakte Rekonstruktion unbekannter Rückseiten oder feiner Texturen. Details sind modellierte Geometrie; keine hochauflösenden Oberflächen- oder Normalmap-Texturen enthalten. build_models.py macht die Geometrie reproduzierbar (Python, NumPy, Matplotlib).

Three.js: const gltf = await new GLTFLoader().loadAsync('/assets/01_stepped_tower.glb'); scene.add(gltf.scene);
React Three Fiber: const { scene } = useGLTF('/assets/01_stepped_tower.glb'); return <primitive object={scene.clone()} />;

preview.png zeigt eine orthografische Übersicht der Modelle. Im Spiel erzeugen Beleuchtung und Schatten zusätzliche Tiefe.

## Fensterkorrektur

110 echte geometrische Aussparungen in den vier Turm-/Modulgebäuden: Fassadendreiecke im Bereich der Öffnungen wurden entfernt, vier seitliche Laibungsflächen ergänzt und Glas bzw. Schachtrückwand nach innen versetzt. Tiefen: 0,035–0,12 Modellmeter. Kuppelgeometrie unverändert. window_recesses.json dokumentiert Lage und Tiefe jeder Aussparung. Die Öffnungen wurden an jeweils neun Punkten darauf geprüft, dass keine Fassadenfläche vor ihnen liegt; GLB-Längen, Buffergrenzen und endliche Koordinaten geprüft.

## Fensterverteilung und Varianten

Alle vier Turm-/Modulgebäude haben Fenster auf Vorderseite, Rückseite und beiden Außenflanken. Der Dreierkomplex hat Fenster auf beiden äußeren Seiten des Gesamtensembles; enge Zwischenräume bleiben Serviceflächen. Die vier Gebäudetypen verwenden unterschiedliche Anordnungen mit schmalen Bändern, zweigeteilten Fenstern, mehrteiligen Querfeldern und langen vertikalen Fenstergruppen. Sprossen sitzen innerhalb der Vertiefungen. Rückseiten sind gestalterische Ergänzungen, da die Referenzen diese Ansichten nicht zeigen. preview_rear.png zeigt die Rückansichten.
