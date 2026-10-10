# Plan B inspired low-poly GLB assets
Five independently modeled, stylized 3D buildings inspired by the supplied screenshots. Not extracted from the original game.

- 01_curved_skyscraper.glb
- 02_modular_complex.glb
- 03_research_monument.glb
- 04_spire_tower.glb
- 05_geodesic_dome.glb

Coordinate system: Z-up (trimesh convention), units arbitrary. Place ground at Z=0. GLB contains vertex colors; use vertexColors materials or standard GLTF loader. For Three.js GLTFLoader, adjust rotation to match your world axes if needed (Three.js defaults Y-up).

Example:
```js
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
const loader = new GLTFLoader();
loader.load('/assets/planb/05_geodesic_dome.glb', ({ scene }) => {
  scene.rotation.x = -Math.PI / 2; // Z-up source -> Y-up Three.js
  world.add(scene);
});
```
