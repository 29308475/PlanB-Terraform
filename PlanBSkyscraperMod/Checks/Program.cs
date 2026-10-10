using System;
using PlanBSkyscraperMod;

internal static class Program
{
    private static void Require(bool value, string message)
    { if (!value) throw new Exception(message); }

    private static int[] Preferences(int x, int y, int seed)
    {
        int parity = y & 1;
        return new[] { VariantRules.Preferred(x + 1, y, seed),
            VariantRules.Preferred(x + parity, y + 1, seed),
            VariantRules.Preferred(x - (1 - parity), y + 1, seed),
            VariantRules.Preferred(x - 1, y, seed),
            VariantRules.Preferred(x - (1 - parity), y - 1, seed),
            VariantRules.Preferred(x + parity, y - 1, seed) };
    }

    private static int Pick(int x, int y, int seed)
        => VariantRules.Choose(x, y, seed, Preferences(x, y, seed));

    private static void Main(string[] args)
    {
        var radii = new System.Collections.Generic.HashSet<float>();
        for (int n = 0; n < 10000; n++)
        {
            float actual = .45f + n * .00001f;
            float cached = FootprintCache.Radius(actual);
            Require(cached <= actual && cached >= actual * .99f, "Cached footprint exceeds a tile or shrinks too far.");
            radii.Add(cached);
        }
        Require(radii.Count <= 23, "Tiny terrain spacing differences still generate excessive model banks.");
        Console.WriteLine($"PASS: 10000 terrain spacings reuse {radii.Count} model sizes, all fit their tiles with less than 1% shrinkage.");
        const int size = 400, seed = 173;
        int[] counts = new int[25];
        int fresh = 0, collisions = 0, baseline = 0, changed = 0;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int choice = Pick(x, y, seed);
            Require(choice >= 0 && choice < 25, "Variant out of range");
            Require(choice == Pick(x, y, seed), "Selection unstable");
            counts[choice]++;
            if (VariantRules.UseNew(x, y, seed, 65)) fresh++;
            Require(!VariantRules.UseNew(x, y, seed, 0), "0% includes variants");
            Require(VariantRules.UseNew(x, y, seed, 100), "100% omits variants");
            if (choice == Pick(x + 1, y, seed)) collisions++;
            if (VariantRules.Preferred(x, y, seed) == VariantRules.Preferred(x + 1, y, seed)) baseline++;
            if (choice != Pick(x, y, seed + 1)) changed++;
        }
        double fraction = fresh / (double)(size * size);
        Require(fraction > .64 && fraction < .66, "Mixture outside expected tolerance");
        foreach (int count in counts) Require(count > 5600 && count < 7200, "Variant distribution too uneven");
        Require(collisions < baseline / 2, "Neighbor selection does not meaningfully reduce repetition");
        Require(changed > size * size * .9, "Seed has insufficient effect");
        foreach (float height in VariantRules.Heights) Require(height >= .5f && height <= 1.65f, "Height outside limits");
        Require(VariantRules.Count == 5 * VariantRules.Heights.Length, "Catalogue size mismatch");
        Console.WriteLine($"PASS: 160000 fields, new share {fraction:P2}, all 25 variants balanced.");
        Console.WriteLine($"Adjacent repeats: {collisions / (double)(size * size):P2} (baseline {baseline / (double)(size * size):P2}).");
        Console.WriteLine("PASS: stable reload selection, seed variation, 0/100% endpoints, minimum height ratios.");
        Require(VariantRules.SkylineBand(0, 0, 12, seed) == 4, "Center is not tallest");
        var skylineCounts = new int[25];
        for (int angle = 0; angle < 360; angle++)
        {
            double a = angle * Math.PI / 180;
            int previous = 4;
            for (int distance = 0; distance <= 40; distance++)
            {
                int band = VariantRules.SkylineBand(Math.Cos(a) * distance, Math.Sin(a) * distance, 12, seed);
                Require(band <= previous, "Height increases away from downtown");
                Require(previous - band <= 1, "Abrupt height jump between nearby buildings");
                previous = band;
            }
            Require(previous == 0, "Outer district is not lowest");
        }
        for (int y = -30; y <= 30; y++)
        for (int x = -30; x <= 30; x++)
        {
            int band = VariantRules.SkylineBand(x, y, 12, seed);
            int variant = VariantRules.ChooseSkyline(x, y, seed, band, Preferences(x, y, seed));
            Require(variant % 5 == band, "Neighbor mixing destroys skyline band");
            Require(variant == VariantRules.ChooseSkyline(x, y, seed, band, Preferences(x, y, seed)), "Skyline is unstable");
            skylineCounts[variant]++;
        }
        Require(VariantRules.SkylineBand(8, 0, 24, seed) > VariantRules.SkylineBand(8, 0, 8, seed), "Radius has no effect");
        Console.WriteLine("PASS: skyline falls off in 360 directions, smooth height bands, all 25 variants available, stable heights despite neighbor mixing.");
        for (int cy = -3; cy <= 3; cy++)
        for (int cx = -3; cx <= 3; cx++)
        {
            VariantRules.ClusterCenter(cx, cy, 12, seed, out double px, out double py);
            Require(VariantRules.MultiSkylineLocalBand(px, py, 12, seed) == 4, "Secondary center is not tallest");
            VariantRules.ClusterCenter(cx + 1, cy, 12, seed, out double nx, out double ny);
            Require(VariantRules.MultiSkylineLocalBand((px + nx) / 2, (py + ny) / 2, 12, seed) <= 1,
                "Clusters merge into a high plateau instead of fading out");
            for (int angle = 0; angle < 360; angle += 10)
            {
                double a = angle * Math.PI / 180;
                int previous = 4;
                for (int d = 0; d <= 12; d++)
                {
                    int band = VariantRules.MultiSkylineLocalBand(px + Math.Cos(a) * d, py + Math.Sin(a) * d, 12, seed);
                    Require(band <= previous, "Local cluster does not fade outwards");
                    Require(previous - band <= 1, "Cluster height jumps too far");
                    previous = band;
                }
                Require(previous <= 1, "Cluster edge remains too tall");
            }
        }
        for (int y = -60; y <= 60; y++)
        for (int x = -60; x <= 60; x++)
        {
            int band = VariantRules.MultiSkylineBand(x, y, 12, seed);
            skylineCounts[VariantRules.ChooseSkyline(x,y,seed,band,Preferences(x,y,seed))]++;
            Require(band == VariantRules.MultiSkylineBand(x, y, 12, seed), "Multi-center layout is unstable");
            Require(Math.Abs(band - VariantRules.MultiSkylineBand(x + 1, y, 12, seed)) <= 1,
                "Cluster boundary introduces a sharp height seam");
        }
        Console.WriteLine("PASS: 49 skyline centers, outward fade in 36 directions each, low valleys, seamless stable multi-center height field.");
        foreach (int count in skylineCounts) Require(count > 0, "Multi-center skyline excludes a variant");
        int highest=0,middle=0,total=0;
        for(int y=-120;y<=120;y++)for(int x=-120;x<=120;x++)
        {
            int band=VariantRules.MultiSkylineBand(x,y,12,seed);
            if(band==4)highest++;
            if(band==1||band==2)middle++;
            total++;
        }
        Require(highest/(double)total<.02,"High accents are too frequent");
        Require(middle/(double)total>.60,"Medium-height groups do not dominate");
        Console.WriteLine($"PASS: calmer height field, highest accents {highest/(double)total:P2}, medium bands {middle/(double)total:P2}; all 25 designs available across clusters.");
        AssetChecks.Validate();
        GlbChecks.Validate();
        if(args.Length==2 && args[0]=="--export")AssetChecks.Export(args[1]);
    }
}
