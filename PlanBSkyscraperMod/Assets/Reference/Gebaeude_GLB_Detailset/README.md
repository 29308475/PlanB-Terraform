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
