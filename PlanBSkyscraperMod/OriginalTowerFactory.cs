using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PlanBSkyscraperMod
{
    // Original models stay recognisable. Five cached templates avoid per-cell mesh copies.
    internal sealed class OriginalTowerFactory
    {
        private readonly Transform parent;
        private readonly Action<string> warning;
        private readonly Dictionary<CAssetModel,CAssetModel[]> cache=new Dictionary<CAssetModel,CAssetModel[]>();
        internal OriginalTowerFactory(Transform parent,Action<string> warning){this.parent=parent;this.warning=warning;}
        internal CAssetModel Get(CAssetModels group,CAssetModel source,int band)
        {
            if(cache.TryGetValue(source,out var models))return models?[band];
            var created=new List<GameObject>();
            try
            {
                float reference=group.models.Where(m=>m.Asset!=null && VariantFactory.IsResidential(m.Asset.name)
                    && m.Asset.name.Contains("_Tower")).Max(m=>VariantFactory.BoundsOf(m.Asset).size.y);
                var bounds=VariantFactory.BoundsOf(source.Asset);
                if(bounds.size.y<=.001f)throw new InvalidOperationException("Invalid original tower height");
                models=new CAssetModel[5];
                for(int i=0;i<5;i++)
                {
                    string suffix=source.Asset.name.Contains("Angle120")?"_Angle120":"";
                    var root=new GameObject(source.Asset.name.Split(new[]{"_Angle"},StringSplitOptions.None)[0]+"_PBSoft"+i+suffix);
                    created.Add(root);root.SetActive(false);root.transform.SetParent(parent,false);root.layer=source.Asset.layer;
                    var body=Object.Instantiate(source.Asset,root.transform);
                    float scale=reference*VariantRules.Heights[i]/bounds.size.y;
                    body.transform.localScale=new Vector3(source.Asset.transform.localScale.x,
                        source.Asset.transform.localScale.y*scale,source.Asset.transform.localScale.z);
                    body.transform.localRotation=Quaternion.identity;
                    body.transform.localPosition=new Vector3(0,bounds.min.y*(1-scale),0);
                    foreach(var lod in body.GetComponentsInChildren<LODGroup>(true))lod.RecalculateBounds();
                    body.SetActive(true);
                    float actual=VariantFactory.BoundsOf(root).size.y;
                    if(Mathf.Abs(actual-reference*VariantRules.Heights[i])>.005f)
                        throw new InvalidOperationException("Original tower height validation failed");
                    models[i]=new CAssetModel(root);
                }
                cache[source]=models;return models[band];
            }
            catch(Exception ex)
            {
                foreach(var go in created)Object.Destroy(go);
                cache[source]=null;warning("Original tower height adaptation failed; preserving original: "+ex);return null;
            }
        }
    }
}
