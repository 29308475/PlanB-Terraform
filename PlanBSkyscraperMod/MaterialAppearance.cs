namespace PlanBSkyscraperMod
{
    // Rendering adjustment only: source GLBs and their stored PBR data remain intact.
    internal static class MaterialAppearance
    {
        internal static float[] Color(string name,float[] source)
        {
            if(name=="glass")return new[]{.08f,.26f,.62f,source[3]};
            float factor=name=="ceramic"?.78f:name=="edge"?.90f:name=="recess"?.65f:name=="metal"?.80f:1f;
            return new[]{source[0]*factor,source[1]*factor,source[2]*factor,source[3]};
        }
    }
}
