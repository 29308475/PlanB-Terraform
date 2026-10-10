import numpy as np, json, struct, math, os, zipfile
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from mpl_toolkits.mplot3d.art3d import Poly3DCollection
OUT=os.path.dirname(__file__)
COLORS=[('ceramic',[.86,.91,.90,1]),('edge',[.97,.98,.93,1]),('glass',[.27,.59,.77,1]),('recess',[.12,.25,.31,1]),('metal',[.49,.61,.64,1]),('yellow',[.95,.85,.33,1])]
class Model:
 def __init__(self,name): self.name=name; self.parts=[]
 def mesh(self,name,v,f,m=0): self.parts.append((name,np.array(v,float),np.array(f,int),m))
 def box(self,name,x,y,z,sx,sy,sz,m=0):
  v=[[x+a*sx/2,y+b*sy/2,z+c*sz/2] for a,b,c in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
  self.mesh(name,v,[[0,2,1],[0,3,2],[4,5,6],[4,6,7],[0,1,5],[0,5,4],[1,2,6],[1,6,5],[2,3,7],[2,7,6],[3,0,4],[3,4,7]],m)
 def profile(self,name,points,y,width,m=0):
  if sum(points[i][0]*points[(i+1)%len(points)][1]-points[(i+1)%len(points)][0]*points[i][1] for i in range(len(points)))>0:points=list(reversed(points))
  n=len(points);v=[[x,y-width/2,z] for x,z in points]+[[x,y+width/2,z] for x,z in points];f=[]
  remaining=list(range(n));cap=[]
  def cross(a,b,c):return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
  while len(remaining)>3:
   for k in range(len(remaining)):
    a,b,c=remaining[k-1],remaining[k],remaining[(k+1)%len(remaining)]
    if cross(points[a],points[b],points[c])>=-1e-9:continue
    if any(all(cross(points[u],points[w],points[t])<=1e-9 for u,w in [(a,b),(b,c),(c,a)]) for t in remaining if t not in [a,b,c]):continue
    cap.append([a,b,c]);remaining.pop(k);break
   else:raise ValueError('Profile triangulation failed')
  cap.append(remaining)
  for a,b,c in cap:f += [[a,c,b],[n+a,n+b,n+c]]
  for i in range(n): j=(i+1)%n;f += [[i,j,n+j],[i,n+j,n+i]]
  self.mesh(name,v,f,m)
 def beam(self,name,a,b,r,m=1,n=6):
  if np.linalg.norm(np.array(b)-np.array(a))<1e-8:return
  a=np.array(a);b=np.array(b);d=(b-a)/np.linalg.norm(b-a);u=np.cross(d,[0,0,1] if abs(d[2])<.9 else [0,1,0]);u/=np.linalg.norm(u);w=np.cross(d,u)
  v=[p+r*(math.cos(t)*u+math.sin(t)*w) for p in [a,b] for t in np.arange(n)*2*math.pi/n];f=[]
  for i in range(n):j=(i+1)%n;f += [[i,j,n+j],[i,n+j,n+i]]
  for i in range(1,n-1):f += [[0,i+1,i],[n,n+i,n+i+1]]
  self.mesh(name,v,f,m)
 def save(self):
  data=bytearray();views=[];acc=[];meshes=[];nodes=[]
  def put(a,target,typ):
   while len(data)%4:data.append(0)
   off=len(data);data.extend(a.tobytes());views.append({'buffer':0,'byteOffset':off,'byteLength':a.nbytes,'target':target});d={'bufferView':len(views)-1,'componentType':5126 if a.dtype==np.float32 else 5125,'count':len(a),'type':typ}
   if typ=='VEC3':d.update(min=a.min(axis=0).tolist(),max=a.max(axis=0).tolist())
   acc.append(d);return len(acc)-1
  for name,v,f,m in self.parts:
   # Flat shading, explicit face normals, independent vertices.
   vv=v[f].reshape(-1,3);nn=np.cross(v[f[:,1]]-v[f[:,0]],v[f[:,2]]-v[f[:,0]]);nn/=np.maximum(np.linalg.norm(nn,axis=1)[:,None],1e-12);nn=np.repeat(nn,3,axis=0)
   # z-up construction -> glTF y-up, right-handed.
   vv=vv[:,[0,2,1]];vv[:,2]*=-1;nn=nn[:,[0,2,1]];nn[:,2]*=-1
   pa=put(vv.astype('float32'),34962,'VEC3');na=put(nn.astype('float32'),34962,'VEC3');ia=put(np.arange(len(vv),dtype='uint32'),34963,'SCALAR')
   meshes.append({'name':name,'primitives':[{'attributes':{'POSITION':pa,'NORMAL':na},'indices':ia,'material':m}]});nodes.append({'name':name,'mesh':len(meshes)-1})
  mats=[{'name':n,'pbrMetallicRoughness':{'baseColorFactor':c,'metallicFactor':.2 if i==4 else 0,'roughnessFactor':.27 if i==2 else .7},'doubleSided':False} for i,(n,c) in enumerate(COLORS)]
  g={'asset':{'version':'2.0','generator':'Reference architectural reconstruction'},'scene':0,'scenes':[{'name':self.name,'nodes':list(range(len(nodes)))}],'nodes':nodes,'meshes':meshes,'materials':mats,'buffers':[{'byteLength':len(data)}],'bufferViews':views,'accessors':acc}
  js=json.dumps(g,separators=(',',':')).encode();js+=b' '*((-len(js))%4);data+=b'\0'*((-len(data))%4)
  with open(os.path.join(OUT,self.name+'.glb'),'wb') as h:h.write(struct.pack('<III',0x46546c67,2,28+len(js)+len(data))+struct.pack('<II',len(js),0x4e4f534a)+js+struct.pack('<II',len(data),0x004e4942)+data)
  return {'file':self.name+'.glb','parts':len(self.parts),'triangles':sum(len(f) for _,v,f,m in self.parts)}
def base(m,x,y):
 m.box('foundation',0,0,.08,x,y,.16,4);m.box('plinth',0,0,.21,x-.12,y-.12,.14,1)
def panel(m,x,y,z,sx,sz):
 m.box('window_reveal',x,y,z,sx+.08,.055,sz+.08,3);m.box('blue_glazing',x,y-.035,z,sx,.025,sz,2)
def tower():
 m=Model('01_stepped_tower');base(m,2.1,1.6)
 p=[(-.86,.28),(.86,.28),(.86,6.6),(.4,6.72),(.05,6.45),(-.08,5.62),(-.3,5.1),(-.55,4.7),(-.86,4.45)]
 m.profile('asymmetric_tower_shell',p,0,1.15)
 m.box('vertical_shadow_channel',.61,-.587,3.54,.12,.06,5.28,3);m.box('vertical_glass_strip',.61,-.625,3.54,.06,.018,5.15,2)
 for k in range(9):panel(m,-.36,-.59,.8+k*.42,.73,.095)
 for z in np.arange(.5,4.45,.42):
  m.box('facade_joint',-.35,-.592,z,.93,.014,.016,4)
 m.box('roof_cap',.57,0,6.61,.55,1.19,.1,1)
 for side in [-1,1]:
  for z in np.arange(1,4.4,.44):m.box('side_window',-.874,side*.27,z,.025,.3,.105,2)
 m.box('service_door',-.56,-.63,.57,.3,.035,.5,4)
 return m
def blocks():
 m=Model('02_three_module_complex');base(m,3.5,2.15)
 for i,x in enumerate([-1.08,0,1.08]):
  h=[2.52,2.88,3.16][i];m.box('module_body',x,0,.3+h/2,.86,1.45,h)
  m.box('front_glazing_channel',x+.19,-.74,.3+h/2,.19,.035,h-.13,3);m.box('front_glass',x+.19,-.765,.3+h/2,.115,.015,h-.18,2)
  m.box('roof_overhang',x,0,h+.35,1.0,1.58,.15,1);m.box('rooftop_plant',x-.16,.16,h+.65,.46,.64,.48,0)
  for z in np.arange(.5,h,.32):m.box('glass_mullion',x+.19,-.782,z,.13,.022,.018,4)
  for y in [-.38,.0,.38]:m.box('side_vertical_seam',x-.442,y,.3+h/2,.018,.02,h-.2,4)
  for j in range(5):m.box('roof_vent_fin',x-.16,-.08+j*.1,h+.9,.34,.022,.02,4)
 for k in range(3):m.box('entry_steps',-.97,-.91-k*.12,.14+k*.05,.61,.17,.1,1)
 m.box('entry_door',-1.18,-.76,.64,.35,.035,.66,3)
 return m
def arch():
 m=Model('03_buttressed_reactor');base(m,3.7,2.3)
 m.box('central_core',0,0,1.93,1.25,1.22,3.3,0)
 for y in [-.635,.635]:
  m.box('central_shaft_recess',0,y,1.95,.41,.07,2.82,3);m.box('yellow_shaft',0,y*1.055,1.95,.2,.03,2.74,5)
  for z in np.arange(.67,3.36,.32):m.box('shaft_crossbar',0,y*1.09,z,.38,.06,.055,4)
  for x in [-.27,.27]:m.box('shaft_rail',x,y*1.05,1.96,.065,.1,2.85,1)
 for s in [-1,1]:
  p=[(s*x,z) for x,z in [(.48,3.6),(.78,3.6),(.94,2.67),(1.19,1.65),(1.61,.52),(1.76,.37),(1.24,.37),(.95,1.28),(.7,2.6)]]
  for y in [-.86,.86]:m.profile('curved_buttress',p,y,.27,1)
  for y in [-.64,.64]:
   m.profile('blue_infill',[(s*.69,.48),(s*1.23,.48),(s*.91,2.85),(s*.69,3.1)],y,.045,2)
   for z in [.85,1.3,1.75,2.2]:m.box('infill_crossbar',s*.85,y,z,.36,.08,.045,1)
 m.box('upper_collar',0,0,3.6,1.7,1.65,.22,1)
 m.profile('split_crown_left',[(-.64,3.7),(-.14,3.7),(-.14,4.82),(-.43,4.7),(-.64,4.06)],0,.79,1)
 m.profile('split_crown_right',[(.14,3.7),(.64,3.7),(.57,4.22),(.32,4.85),(.14,4.82)],0,.79,0)
 m.box('crown_slot',0,0,4.18,.18,.64,.85,3)
 for x in np.arange(-1.55,1.65,.26):panel(m,x,-1.10,.37,.15,.12)
 return m
def spire():
 m=Model('04_antenna_spire');base(m,2.5,2.3)
 m.box('lower_core',0,0,1.12,.97,.91,1.65)
 for s in [-1,1]:
  p=[(s*x,z) for x,z in [(.36,2.03),(.63,1.9),(1.06,.4),(1.23,.27),(.71,.27),(.43,1.18)]]
  for y in [-.45,.45]:m.profile('splayed_leg',p,y,.23,1)
 for y in [-.47,.47]:
  for x in [-.24,.0,.24]:m.box('lower_glass',x,y,1.44,.13,.03,.71,2)
 m.box('upper_shaft',0,0,2.92,.68,.72,2.2)
 for k in range(6):
  for y in [-.373,.373]:m.box('shaft_window_band',-.04,y,2.02+k*.31,.4,.025,.095,2)
 m.box('shaft_cap',0,0,4.05,.76,.8,.15,1)
 m.profile('antenna_fin',[(-.2,4.13),(.2,4.13),(.14,4.95),(.03,5.25),(-.09,5.18)],0,.25,1)
 m.box('antenna_channel',.0,-.135,4.64,.044,.014,.73,2)
 m.box('entry_recess',0,-.486,.58,.4,.05,.62,3)
 return m
def dome():
 m=Model('05_geodesic_dome');base(m,3.15,3.15)
 # Subdivided icosahedron clipped precisely to its equator.
 phi=(1+5**.5)/2;v=np.array([[-1,phi,0],[1,phi,0],[-1,-phi,0],[1,-phi,0],[0,-1,phi],[0,1,phi],[0,-1,-phi],[0,1,-phi],[phi,0,-1],[phi,0,1],[-phi,0,-1],[-phi,0,1]],float);v/=np.linalg.norm(v,axis=1)[:,None]
 f=[[0,11,5],[0,5,1],[0,1,7],[0,7,10],[0,10,11],[1,5,9],[5,11,4],[11,10,2],[10,7,6],[7,1,8],[3,9,4],[3,4,2],[3,2,6],[3,6,8],[3,8,9],[4,9,5],[2,4,11],[6,2,10],[8,6,7],[9,8,1]]
 fs=[]
 for a,b,c in f:
  aa,bb,cc=v[a],v[b],v[c];ab=aa+bb;ab/=np.linalg.norm(ab);bc=bb+cc;bc/=np.linalg.norm(bc);ca=cc+aa;ca/=np.linalg.norm(ca)
  fs += [[aa,ab,ca],[bb,bc,ab],[cc,ca,bc],[ab,bc,ca]]
 edges={}
 for tri in fs:
  poly=[]
  for a,b in zip(tri,tri[1:]+tri[:1]):
   if a[2]>=-1e-8:poly.append(a)
   if (a[2]<0)!=(b[2]<0):poly.append(a+(b-a)*(-a[2]/(b[2]-a[2])))
  if len(poly)<3:continue
  pp=[np.array([p[0]*1.45,p[1]*1.45,.31+p[2]*1.45]) for p in poly]
  center=np.mean(pp,axis=0);inset=[center+.91*(p-center) for p in pp]
  m.mesh('triangular_glass_panel',inset,[[0,i,i+1] for i in range(1,len(pp)-1)],2)
  for a,b in zip(pp,pp[1:]+pp[:1]):
   key=tuple(sorted((tuple(np.round(a,5)),tuple(np.round(b,5)))))
   edges[key]=(a,b)
 for a,b in edges.values():m.beam('geodesic_structural_strut',a,b,.046,1)
 for i in range(32):
  a=i*2*np.pi/32;b=(i+1)*2*np.pi/32;m.beam('equatorial_ring',[1.45*np.cos(a),1.45*np.sin(a),.31],[1.45*np.cos(b),1.45*np.sin(b),.31],.067,1)
 m.box('airlock',0,-1.41,.5,.47,.39,.42,1);m.box('airlock_glass',0,-1.612,.49,.3,.025,.29,3)
 return m
models=[tower(),blocks(),arch(),spire(),dome()];stats=[m.save() for m in models]
fig=plt.figure(figsize=(16,9),facecolor='#e6eef1')
for i,m in enumerate(models):
 ax=fig.add_subplot(2,3,i+1,projection='3d');ax.set_facecolor('#e6eef1')
 triangles=[];colors=[]
 for name,v,f,ma in m.parts:
  triangles.extend(v[f]);norm=np.cross(v[f[:,1]]-v[f[:,0]],v[f[:,2]]-v[f[:,0]]);norm/=np.maximum(np.linalg.norm(norm,axis=1)[:,None],1e-12);light=np.array([-.3,-.5,.8]);brightness=.68+.32*np.maximum(norm@light,0);colors.extend(np.array(COLORS[ma][1][:3])[None,:]*brightness[:,None])
 pc=Poly3DCollection(triangles,facecolors=colors,edgecolors='none',zsort='average');ax.add_collection3d(pc)
 allv=np.concatenate([v for _,v,f,ma in m.parts]);lo=allv.min(0);hi=allv.max(0);mid=(lo+hi)/2;rad=max(hi-lo)/2
 ax.set_xlim(mid[0]-rad,mid[0]+rad);ax.set_ylim(mid[1]-rad,mid[1]+rad);ax.set_zlim(0,2*rad);ax.set_box_aspect([1,1,1]);ax.view_init(22,-65);ax.set_axis_off();ax.set_title(m.name.replace('_',' '),fontsize=12)
plt.tight_layout();plt.savefig(os.path.join(OUT,'preview.png'),dpi=160);plt.close()
with open(os.path.join(OUT,'model_stats.json'),'w') as h:json.dump(stats,h,indent=2)
print(json.dumps(stats,indent=2))
