import trimesh, numpy as np, math, os
from trimesh.transformations import rotation_matrix
OUT=os.path.dirname(__file__)
C={'white':[239,245,241,255],'ivory':[255,251,235,255],'blue':[77,165,220,255],'glass':[90,185,241,255],'dark':[41,112,168,255],'base':[160,181,194,255],'lime':[202,215,120,255],'gold':[251,221,103,255]}
class Model:
 def __init__(self):self.parts=[]
 def add(self,mesh,color):
  mesh.visual.vertex_colors=np.tile(np.array(C[color],dtype=np.uint8),(len(mesh.vertices),1));self.parts.append(mesh)
 def box(self,pos,size,color):
  m=trimesh.creation.box(extents=size);m.apply_translation(pos);self.add(m,color)
 def cyl(self,pos,r,h,color,sections=12):
  m=trimesh.creation.cylinder(radius=r,height=h,sections=sections);m.apply_translation(pos);self.add(m,color)
 def tapered(self,z0,z1,w0,d0,w1,d1,color,center=(0,0)):
  x,y=center; v=np.array([[x+a*w/2,y+b*d/2,z] for z,w,d in [(z0,w0,d0),(z1,w1,d1)] for a,b in [(-1,-1),(1,-1),(1,1),(-1,1)]],float)
  f=[[0,2,1],[0,3,2],[4,5,6],[4,6,7]]
  for i in range(4):j=(i+1)%4;f.extend([[i,j,4+j],[i,4+j,4+i]])
  self.add(trimesh.Trimesh(vertices=v,faces=f,process=False),color)
 def beam(self,a,b,r,color,sections=6):
  a=np.array(a,float);b=np.array(b,float);v=b-a;length=np.linalg.norm(v)
  if length<1e-5:return
  m=trimesh.creation.cylinder(radius=r,height=length,sections=sections)
  m.apply_transform(trimesh.geometry.align_vectors([0,0,1],v/length));m.apply_translation((a+b)/2);self.add(m,color)
 def export(self,name):
  s=trimesh.Scene();
  for i,p in enumerate(self.parts):s.add_geometry(p,geom_name=f'{name}_part_{i:03d}')
  s.export(os.path.join(OUT,name+'.glb'));print(name,len(self.parts))
# tower with sweeping stepped silhouette
m=Model();m.box((0,0,.13),(3.2,2.4,.26),'base');m.box((-.7,0,1.0),(1.4,2,1.7),'white');m.box((-.7,-1.03,1),(1,.07,1.15),'glass')
m.tapered(0.28,8.6,2.45,1.9,1.35,1.35,'ivory',(.48,0));m.box((.55,0,8.65),(1.45,1.38,.24),'white')
for z in np.arange(1.1,5.9,.72):m.box((.5,-.992,z),(1.55,.055,.24),'blue')
for x in [1.0,1.45]:m.box((x,-.72,5.9),(.22,.06,4.8),'dark')
m.export('01_curved_skyscraper')
# modular building
m=Model();m.box((0,0,.13),(5.7,3.2,.26),'base')
for x,h in [(-1.65,3.8),(-.15,4.45),(1.35,5.0)]:
 m.box((x,0,.28+h/2),(1.3,1.85,h),'white');m.box((x,-.957,.35+h/2),(.73,.065,h-.5),'blue');m.box((x,.0,.3+h),(1.0,1.5,.14),'ivory');m.box((x,.0,.48+h),(.68,.7,.3),'lime')
for z in [1.15,2.15]:m.box((-2.5,-1.05,z),(.7,1.05,.22),'ivory')
m.export('02_modular_complex')
# monumental research hall
m=Model();m.box((0,0,.15),(7.6,3.5,.3),'base');m.box((0,0,1.2),(6.8,2.7,1.9),'white');m.box((0,-1.39,1.22),(6.4,.06,1.28),'glass')
m.tapered(2.05,6.7,6.5,2.6,2.8,1.6,'ivory');m.tapered(2.1,6.3,4.5,2.68,2.0,1.64,'glass')
for x in [-3.2,-1.65,1.65,3.2]:m.beam((x,-1.48,2.1),(x*.44,-.92,6.75),.19,'white')
for x in [-.3,.3]:m.box((x,-1.48,4.25),(.24,.08,3.6),'gold')
m.box((0,0,6.9),(3.0,1.85,.45),'white')
for x in [-.65,.65]:m.tapered(7.12,8.55,.9,1.4,.65,.95,'white',(x,0))
for x in [-3.4,3.4]:m.tapered(.3,2.5,.9,.9,.35,.4,'white',(x,-1.0))
m.export('03_research_monument')
# spire
m=Model();m.box((0,0,.12),(4.2,4.2,.24),'base');m.tapered(.24,3.3,3.7,3.7,1.8,1.8,'white');m.tapered(3.3,7.0,1.85,1.85,1.05,1.05,'white');m.tapered(7,9.3,1.05,1.05,.24,.24,'ivory')
for z in [4.0,4.85,5.7,6.55]:
 width=1.8-(z-3.3)*.2;m.box((0,-width/2-.035,z),(width*.65,.065,.23),'blue')
for x in [-1,0,1]:m.beam((x*.75,-1.89,.55),(x*.34,-.92,3.15),.1,'blue')
m.export('04_spire_tower')
# dome triangulated geodesic hemisphere
m=Model();m.cyl((0,0,.12),3.2,.24,'base',32)
sphere=trimesh.creation.icosphere(subdivisions=2,radius=3.05)
verts=sphere.vertices;faces=sphere.faces;edges=set()
for face in faces:
 if all(verts[i,2]>=-.02 for i in face):
  for i,j in [(face[0],face[1]),(face[1],face[2]),(face[2],face[0])]:edges.add(tuple(sorted((int(i),int(j)))))
for i,j in sorted(edges):
 a=verts[i].copy();b=verts[j].copy();a[2]+=.28;b[2]+=.28;m.beam(a,b,.085,'white',5)
for face in faces:
 if all(verts[i,2]>=.0 for i in face):
  vv=verts[face].copy();vv[:,2]+=.28; center=vv.mean(axis=0);vv=center+(vv-center)*.94
  mesh=trimesh.Trimesh(vertices=vv,faces=[[0,1,2],[2,1,0]],process=False);m.add(mesh,'glass')
m.export('05_geodesic_dome')
