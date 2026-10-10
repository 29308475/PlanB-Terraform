from pathlib import Path
import json,struct,zipfile
import numpy as np
import geometry as g
from PIL import Image,ImageDraw,ImageFilter
HERE=Path(__file__).resolve().parent;g.OUT=str(HERE)
m=g.Model('06_freight_station');m.pockets=[]
def box(name,x,y,z,sx,sy,sz,ma=0):m.box(name,x,y,z,sx,sy,sz,ma)
def window(wall,axis,sign,surface,u,z,w,h,depth=.11,cols=1,rows=1):g.divided_window(m,wall,axis,sign,surface,u,z,w,h,depth,cols,rows)
# Foundation, yard and uninterrupted rail beds. Coordinates are z-up until GLB export.
box('station_foundation',0,0,.09,14,9,.18,4)
box('rear_service_court',0,3.67,.205,12.9,1.37,.05,0)
box('loading_platform',.8,-.45,.43,10.9,1.4,.5,0)
box('platform_edge',.8,-1.11,.68,10.9,.09,.08,1)
for x in np.arange(-4.3,6.0,.45):box('yellow_platform_safety_mark',x,-1.11,.725,.23,.075,.01,5)
# Main terminal, rectangular pockets are subtracted from solid walls.
box('cargo_hall',1.45,1.65,1.70,7.6,2.8,2.84,0)
box('hall_roof_cap',1.45,1.65,3.16,7.94,3.08,.16,1)
for x in [-1.25,.85,2.95,4.65]:
 width=1.34 if x<4 else .76
 g.pocket(m,'cargo_hall',1,-1,.25,x,1.52,width,1.58,.20,3)
 box('inset_loading_shutter',x,.44,1.52,width-.12,.025,1.47,4)
 for z in np.arange(.85,2.25,.14):box('shutter_horizontal_slat',x,.417,z,width-.12,.025,.026,0)
 for dx in [-width/2-.075,width/2+.075]:box('loading_portal_side',x+dx,.235,1.52,.105,.09,1.8,1)
 box('loading_portal_header',x,.22,2.40,width+.26,.15,.15,1)
 box('portal_status_blue',x+.3,.135,2.40,.13,.028,.055,2)
 window('cargo_hall',1,-1,.25,x,2.76,width,.26,.105,cols=3 if width>1 else 2)
# Windows along the rear and both ends, varied but aligned with the bay grid.
for x in [-1.25,.85,2.95,4.65]:
 window('cargo_hall',1,1,3.05,x,2.64,1.15,.24,.12,cols=3)
 window('cargo_hall',1,1,3.05,x,1.47,.66,.92,.12,cols=2,rows=2)
for sign,surface in [(-1,-2.35),(1,5.25)]:
 for y in [1.05,2.28]:window('cargo_hall',0,sign,surface,y,2.56,.65,.30,.11,cols=2)
 window('cargo_hall',0,sign,surface,1.65,1.42,.24,1.08,.12,rows=3)
# Tall control office with the same asymmetrical silhouette as the earlier tower.
p=[(-5.5,.28),(-3.6,.28),(-3.6,4.95),(-4.13,5.08),(-4.60,4.94),(-4.74,4.35),(-5.1,3.86),(-5.5,3.62)]
m.profile('control_tower',p,1.7,2.4,0)
for sign,surface in [(-1,.5),(1,2.9)]:
 window('control_tower',1,sign,surface,-3.92,2.71,.16,3.55,.12,rows=6)
 for k,z in enumerate([1.23,1.82,2.41,3.0]):window('control_tower',1,sign,surface,-4.83,z,.84,.20 if k%2==0 else .34,.12,cols=3 if k%2 else 1)
 window('control_tower',1,sign,surface,-4.28,4.39,.42,.32,.13,cols=2)
for sign,surface in [(-1,-5.5),(1,-3.6)]:
 for z in [1.35,2.15,2.95]:window('control_tower',0,sign,surface,1.7,z,1.55,.27,.12,cols=4)
g.pocket(m,'control_tower',1,-1,.5,-4.92,.66,.58,.64,.15,3)
box('entrance_canopy',-4.92,.21,1.13,.93,.78,.10,1)
for k in range(4):box('office_entry_step',-4.92,.2-k*.20,.23+k*.06,.85,.24,.10,1)
box('tower_roof_cap',-3.97,1.7,5.00,.72,2.50,.12,1)
# Low connecting corridor.
box('office_hall_link',-2.96,1.76,1.23,1.29,1.45,1.90,0)
box('link_roof',-2.96,1.76,2.22,1.4,1.57,.12,1)
for sign,surface in [(-1,1.035),(1,2.485)]:window('office_hall_link',1,sign,surface,-2.96,1.50,.90,.36,.10,cols=3)
# Floating loading canopy, tapered supports and roof plant.
box('loading_canopy',1.45,-.40,2.58,8.02,1.68,.14,1)
box('canopy_blue_fascia',1.45,-1.235,2.58,7.65,.02,.045,2)
for x in [-2.37,5.25]:
 m.profile('tapered_canopy_support',[(x-.12,.70),(x+.12,.70),(x+.09,2.50),(x-.02,2.57),(x-.12,2.2)],-.95,.19,1)
for x in [-.85,1.5,3.85]:
 box('roof_vent_plinth',x,2.06,3.29,.87,.90,.11,4)
 box('roof_vent_housing',x,2.06,3.46,.75,.78,.27,0)
 for y in np.arange(1.77,2.39,.095):box('roof_vent_louver',x,y,3.61,.62,.032,.025,4)
 box('roof_skylight_frame',x,.96,3.32,1.28,.75,.18,1)
 box('roof_skylight_blue_pane',x,.96,3.418,1.06,.54,.016,2)
 for xx in [x-.26,x+.26]:box('skylight_mullion',xx,.96,3.44,.03,.55,.025,1)
# Two through tracks; ends at x +/-7 for direct extension in game.
for t,y in enumerate([-2.05,-3.63]):
 box('track_ballast_'+str(t),0,y,.22,14,1.14,.08,4)
 for x in np.arange(-6.9,6.96,.28):box('track_sleeper_'+str(t),x,y,.282,.11,.94,.06,3)
 for offset in [-.32,.32]:
  box('rail_foot_'+str(t),0,y+offset,.32,14,.065,.025,4)
  box('rail_web_'+str(t),0,y+offset,.36,14,.028,.065,4)
  box('rail_head_'+str(t),0,y+offset,.40,14,.055,.025,1)
# Rail-side transfer gantry: feet outside both track envelopes.
for y in [-4.32,-1.34]:
 box('gantry_foot',4.05,y,.30,.62,.35,.24,4)
 box('gantry_column',4.05,y,1.81,.23,.20,2.86,1)
 for z in [.7,1.6,2.5]:box('gantry_column_inset',4.045,y-.107,z,.10,.02,.45,2)
box('transfer_gantry_crossbeam',4.05,-2.83,3.34,.44,3.45,.27,1)
box('gantry_travel_rail',4.05,-2.83,3.16,.17,3.22,.08,4)
box('gantry_trolley',4.05,-2.05,3.09,.36,.43,.14,5)
for x in [3.96,4.14]:m.beam('hoist_cable',[x,-2.05,3.03],[x,-2.05,2.24],.014,3)
box('lifting_spreader',4.05,-2.05,2.20,.52,.39,.12,5)
# Cargo modules with solid rib geometry, optional static wagon for scale.
def container(prefix,x,y,z,length=1.42,width=.81,height=.83,ma=2):
 box(prefix+'_body',x,y,z,length,width,height,ma)
 for xx in [x-length/2,x+length/2]:
  for yy in [y-width/2,y+width/2]:box(prefix+'_corner',xx,yy,z,.055,.055,height+.055,1)
 for xx in np.arange(x-length/2+.13,x+length/2-.08,.15):
  for sign in [-1,1]:box(prefix+'_rib',xx,y+sign*(width/2+.013),z,.025,.026,height-.10,4)
 for sign in [-1,1]:box(prefix+'_top_edge',x,y+sign*width/2,z+height/2,length,.045,.045,1)
for x in [-.5,1.22]:container('platform_cargo',x,-.52,1.13,1.35,.70,.83)
container('rear_cargo',-.7,3.90,.71,1.40,.78,.90,4)
container('rear_cargo',1.04,3.90,.65,1.40,.78,.78,2)
# A single detachable static flat wagon, all node names start with wagon_.
wx,wy=-1.65,-3.63
box('wagon_chassis',wx,wy,.73,2.85,.82,.16,4)
box('wagon_deck',wx,wy,.835,2.9,.91,.055,1)
for x in [wx-.88,wx+.88]:
 m.beam('wagon_axle',[x,wy-.37,.575],[x,wy+.37,.575],.04,4,10)
 for y in [wy-.32,wy+.32]:m.beam('wagon_wheel',[x,y-.045,.575],[x,y+.045,.575],.17,3,14)
for x in [wx-1.53,wx+1.53]:box('wagon_coupler',x,wy,.71,.24,.14,.08,3)
container('wagon_cargo',wx,wy,1.32,2.53,.77,.91)
# Minimal wayfinding and warning bollards.
for x in [-2.3,5.25]:
 box('platform_bollard',x,-1.1,.90,.08,.08,.4,4)
 box('bollard_yellow_cap',x,-1.1,1.10,.1,.1,.065,5)
box('station_sign_frame',-4.30,.36,3.43,.76,.10,.23,1)
box('station_sign_blue',-4.30,.30,3.43,.61,.018,.12,2)
# Export explicit named nodes, standard PBR, y-up and ground-level pivot.
# Confirm each aperture is genuinely clear at the original wall surface.
for p in m.pockets:
 axis=p['axis'];tangent=1-axis
 for name,v,f,ma in m.parts:
  if name!=p['wall']:continue
  tri=v[f];tri=tri[np.all(np.abs(tri[:,:,axis]-p['surface'])<1e-8,axis=1)]
  for du in [-.3,0,.3]:
   for dz in [-.3,0,.3]:
    point=np.array([p['u']+du*p['width'],p['z']+dz*p['height']])
    for face in tri[:,:,[tangent,2]]:
     a,b,c=face;matrix=np.column_stack([b-a,c-a])
     if abs(np.linalg.det(matrix))<1e-12:continue
     uv=np.linalg.solve(matrix,point-a)
     assert not (min(uv)>-1e-8 and sum(uv)<1+1e-8),('Blocked aperture',p)
# Merge static pieces by functional group and material, retaining optional wagon.
original=m.parts;groups={}
for name,v,f,ma in original:
 if name.startswith('wagon_'):group='wagon'
 elif name.startswith(('track_','rail_')):group='tracks'
 elif name.startswith(('gantry_','transfer_gantry','hoist_','lifting_')):group='gantry'
 elif 'cargo' in name and name!='cargo_hall':group='cargo'
 elif name.startswith(('roof_','skylight_')):group='roof_equipment'
 else:group='station'
 groups.setdefault((group,ma),[]).append((v,f))
m.parts=[]
for (group,ma),pieces in groups.items():
 vertices=[];faces=[];offset=0
 for v,f in pieces:vertices.append(v);faces.append(f+offset);offset+=len(v)
 m.mesh(group+'_'+g.COLORS[ma][0],np.concatenate(vertices),np.concatenate(faces),ma)
stats=m.save();stats.update(source_parts=len(original),window_openings=len(m.pockets))
(HERE/'model_stats.json').write_text(json.dumps(stats,indent=2));(HERE/'window_recesses.json').write_text(json.dumps(m.pockets,indent=2))
file=HERE/'06_freight_station.glb';raw=file.read_bytes();assert struct.unpack_from('<III',raw)[:2]==(0x46546c67,2);assert struct.unpack_from('<I',raw,8)[0]==len(raw)
size=struct.unpack_from('<I',raw,12)[0];doc=json.loads(raw[20:20+size]);blob=raw[28+size:]
for a in doc['accessors']:
 view=doc['bufferViews'][a['bufferView']];assert view['byteOffset']+view['byteLength']<=len(blob)
 if a['componentType']==5126:assert np.isfinite(np.frombuffer(blob[view['byteOffset']:view['byteOffset']+view['byteLength']],dtype='<f4')).all()

# Orthographic software rasterizer with a z-buffer: no triangle-sort artifacts.
def render(filename,azimuth=-58,elevation=29):
 width,height=1600,1100
 az,el=np.radians([azimuth,elevation]);camera=np.array([np.cos(az)*np.cos(el),np.sin(az)*np.cos(el),np.sin(el)])
 right=np.cross([0,0,1],camera);right/=np.linalg.norm(right);up=np.cross(camera,right)
 verts=np.concatenate([p[1] for p in m.parts]);proj=np.column_stack([verts@right,verts@up]);low=proj.min(0);high=proj.max(0);scale=min((width-180)/(high[0]-low[0]),(height-180)/(high[1]-low[1]));mid=(low+high)/2
 rgb=np.zeros((height,width,3),dtype=np.uint8);rgb[:]=[225,235,239];zbuf=np.full((height,width),-np.inf)
 light=np.array([-.38,-.55,.75]);light/=np.linalg.norm(light)
 for name,v,faces,material in m.parts:
  screen=np.column_stack([(v@right-mid[0])*scale+width/2,height/2-(v@up-mid[1])*scale]);depth=v@camera
  for face in faces:
   tri=v[face];normal=np.cross(tri[1]-tri[0],tri[2]-tri[0]);length=np.linalg.norm(normal)
   if length<1e-10:continue
   normal/=length
   if normal@camera<=0:continue
   q=screen[face];xmin=max(0,int(np.floor(q[:,0].min())));xmax=min(width-1,int(np.ceil(q[:,0].max())));ymin=max(0,int(np.floor(q[:,1].min())));ymax=min(height-1,int(np.ceil(q[:,1].max())))
   if xmax<xmin or ymax<ymin:continue
   a,b,c=q;den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
   if abs(den)<1e-9:continue
   xx,yy=np.meshgrid(np.arange(xmin,xmax+1)+.5,np.arange(ymin,ymax+1)+.5)
   wa=((b[1]-c[1])*(xx-c[0])+(c[0]-b[0])*(yy-c[1]))/den;wb=((c[1]-a[1])*(xx-c[0])+(a[0]-c[0])*(yy-c[1]))/den;wc=1-wa-wb
   zz=wa*depth[face[0]]+wb*depth[face[1]]+wc*depth[face[2]];buf=zbuf[ymin:ymax+1,xmin:xmax+1];mask=(wa>=-1e-8)&(wb>=-1e-8)&(wc>=-1e-8)&(zz>buf)
   shade=.57+.43*max(0,normal@light);color=np.clip(np.array(g.COLORS[material][1][:3])*255*shade,0,255).astype(np.uint8)
   rgb[ymin:ymax+1,xmin:xmax+1][mask]=color;buf[mask]=zz[mask]
 Image.fromarray(rgb).save(HERE/filename)
render('preview_front.png');render('preview_rear.png',122,31)
print(json.dumps(stats))
