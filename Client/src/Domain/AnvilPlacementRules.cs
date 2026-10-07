using System;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using System.IO.Compression;

namespace LibreKO.Domain;

// Mirrors the fork's scroll classes and protection rules. Final recipe validation stays on the server.
public static class AnvilPlacementRules
{
    private enum Scroll { None, Low, Middle, High, ClassUpgrade, Reverse, Rebirth, Accessory }
    private sealed record Setting(int ReqItem1,int ReqItem2,int ItemType,int ItemGrade,int SuccessRate);
    private static readonly Lazy<Setting[]> Settings=new(()=>
    {
        using var stream=typeof(AnvilPlacementRules).Assembly.GetManifestResourceStream("LibreKO.AnvilUpgradeSettings")!;
        return JsonSerializer.Deserialize<Setting[]>(stream)!;
    });
    private static readonly Lazy<Dictionary<int,int[]>> Origins=new(()=>
    {
        using var source=typeof(AnvilPlacementRules).Assembly.GetManifestResourceStream("LibreKO.AnvilOrigins")!;
        using var stream=new GZipStream(source,CompressionMode.Decompress);
        return JsonSerializer.Deserialize<int[][]>(stream)!.ToDictionary(row=>row[0]);
    });
    public static bool Allows(ReadOnlySpan<int> items,bool compound)
    {
        if(items[0]==0)return Allows(items,compound,0,0,0,false);
        if(!Origins.Value.TryGetValue(items[0],out var origin))return false;
        bool hasScroll=false,hasProtection=false;
        for(int i=compound?3:1;i<items.Length;i++)
        {
            if(Classify(items[i])!=Scroll.None)
            {if(!origin.AsSpan(5).Contains(items[i]))return false;hasScroll=true;}
            else if(items[i]!=0)hasProtection=true;
        }
        if(!hasScroll&&hasProtection)
        {
            var proposed=items.ToArray();int slot=Array.FindIndex(proposed,compound?3:1,id=>id==0);
            if(slot<0)return false;
            foreach(int scroll in origin.AsSpan(5))
            {
                proposed[slot]=scroll;
                if(Allows(proposed,compound,origin[1],origin[2],origin[3],origin[4] is 91 or 92 or 93 or 94))return true;
            }
            return false;
        }
        return Allows(items,compound,origin[1],origin[2],origin[3],origin[4] is 91 or 92 or 93 or 94);
    }
    private static Scroll Classify(int id)=>id switch
    {
        >=379016000 and <=379035000 or >=379138000 and <=379141000=>Scroll.High,
        379152000=>Scroll.ClassUpgrade,
        >=379159000 and <=379164000=>Scroll.Accessory,
        >=379205000 and <=379220000=>Scroll.Middle,
        >=379221000 and <=379235000 or 379255000=>Scroll.Low,
        379256000=>Scroll.Reverse,379257000=>Scroll.Rebirth,_=>Scroll.None
    };
    private static bool Protection(int id)=>id is 700002000 or 379258000 or 352900000 or 353000000 or 354000000 or 890092000;
    public static bool IsMaterial(int id,bool compound)
    {
        bool role=compound ? Classify(id)==Scroll.Accessory || id==354000000
            : (Classify(id) is not (Scroll.None or Scroll.Accessory)) || (Protection(id)&&id!=354000000);
        return role && (id==890092000 || Settings.Value.Any(s=>s.SuccessRate>0&&(s.ReqItem1==id||s.ReqItem2==id)));
    }

    public static bool Allows(ReadOnlySpan<int> items,bool compound,int itemClass,int itemType,int grade,bool accessory)
    {
        int scroll=0,protection=0;
        for(int i=compound?3:1;i<items.Length;i++)
        {
            int id=items[i];if(id==0)continue;
            if(!IsMaterial(id,compound))return false;
            if(Classify(id)!=Scroll.None){if(scroll!=0)return false;scroll=id;}
            else {if(protection!=0)return false;protection=id;}
        }
        var category=Classify(scroll);
        if(protection==890092000 && category is Scroll.Reverse or Scroll.Accessory)return false;
        if(scroll!=0 && protection!=0 && protection!=890092000 && !Settings.Value.Any(s=>s.SuccessRate>0 && Materials(s,scroll,protection)))return false;
        if(items[0]==0)return true;
        if(compound && !accessory)return false;
        if(!compound && accessory)return false;
        if(scroll!=0 && !compound && !AllowedClass(itemClass,category))return false;
        if(protection==890092000 && (grade>=10 || itemType is not (4 or 5 or 11 or 12)))return false;
        if(scroll==0)
            return protection==0 || protection==890092000 || Settings.Value.Any(s=>s.ItemType==itemType && (s.ItemGrade==grade||s.ItemGrade==99) && s.SuccessRate>0 && (s.ReqItem1==protection||s.ReqItem2==protection));
        if(protection==890092000)return true;
        // With a scroll alone, allow a supported protection to be added afterward.
        var settings=Settings.Value.Where(s=>s.ItemType==itemType && (s.ItemGrade==grade||s.ItemGrade==99));
        if(protection==0)return settings.Any(s=>s.SuccessRate>0 && (s.ReqItem1==scroll||s.ReqItem2==scroll));
        return settings.FirstOrDefault(s=>Materials(s,scroll,protection)) is { SuccessRate: >0 };
    }
    private static bool Materials(Setting s,int a,int b)=>(s.ReqItem1==a||s.ReqItem2==a)&&(s.ReqItem1==b||s.ReqItem2==b);
    private static bool AllowedClass(int itemClass,Scroll scroll)=>itemClass switch
    {
        0 or 1=>scroll is Scroll.Low or Scroll.Middle or Scroll.High or Scroll.ClassUpgrade,
        2=>scroll is Scroll.Middle or Scroll.High or Scroll.ClassUpgrade,
        3 or 7 or 33=>scroll is Scroll.High or Scroll.Reverse or Scroll.ClassUpgrade,
        4 or 5 or 35=>scroll is Scroll.Rebirth or Scroll.Reverse or Scroll.High,
        _=>false
    };
}
