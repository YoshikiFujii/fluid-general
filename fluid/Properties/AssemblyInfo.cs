using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;

// 繧｢繧ｻ繝ｳ繝悶Μ縺ｫ髢｢縺吶ｋ荳闊ｬ逧・↑諠・ｱ縺ｯ縲∵ｬ｡縺ｮ譁ｹ豕輔〒蛻ｶ蠕｡縺輔ｌ縺ｾ縺・
// 繧｢繧ｻ繝ｳ繝悶Μ縺ｫ髢｢騾｣莉倥￠繧峨ｌ縺ｦ縺・ｋ諠・ｱ繧貞､画峩縺吶ｋ縺ｫ縺ｯ縲・
// 縺薙ｌ繧峨・螻樊ｧ蛟､繧貞､画峩縺励※縺上□縺輔＞縲・
[assembly: AssemblyTitle("fluid-general")]
[assembly: AssemblyDescription("蟄ｦ逕溷ｯｮ縺ｮ蜷咲ｰｿ邂｡逅・所縺ｳ蜃ｺ蟶ｭ遒ｺ隱咲畑繧｢繝励Μ繧ｱ繝ｼ繧ｷ繝ｧ繝ｳ")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("蜊・痩蟾･讌ｭ螟ｧ蟄ｦ蟄ｦ逕溷ｯｮ蟇ｮ蜿倶ｼ壼濤陦悟ｽｹ蜩｡莨・)]
[assembly: AssemblyProduct("fluid-general")]
[assembly: AssemblyCopyright("Copyright ﾂｩ Yoshiki.F")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// ComVisible 繧・false 縺ｫ險ｭ螳壹☆繧九→縲√％縺ｮ繧｢繧ｻ繝ｳ繝悶Μ蜀・・蝙九・ COM 繧ｳ繝ｳ繝昴・繝阪Φ繝医°繧・
// 蜿ら・縺ｧ縺阪↑縺上↑繧翫∪縺吶・OM 縺九ｉ縺薙・繧｢繧ｻ繝ｳ繝悶Μ蜀・・蝙九↓繧｢繧ｯ繧ｻ繧ｹ縺吶ｋ蠢・ｦ√′縺ゅｋ蝣ｴ蜷医・縲・
// 縺昴・蝙九・ ComVisible 螻樊ｧ繧・true 縺ｫ險ｭ螳壹＠縺ｦ縺上□縺輔＞縲・
[assembly: ComVisible(false)]

//繝ｭ繝ｼ繧ｫ繝ｩ繧､繧ｺ蜿ｯ閭ｽ縺ｪ繧｢繝励Μ繧ｱ繝ｼ繧ｷ繝ｧ繝ｳ縺ｮ繝薙Ν繝峨ｒ髢句ｧ九☆繧九↓縺ｯ縲・
//.csproj 繝輔ぃ繧､繝ｫ縺ｮ <UICulture>CultureYouAreCodingWith</UICulture> 繧・
//<PropertyGroup> 蜀・Κ縺ｧ險ｭ螳壹＠縺ｾ縺吶ゅ◆縺ｨ縺医・縲・
//繧ｽ繝ｼ繧ｹ 繝輔ぃ繧､繝ｫ縺ｧ闍ｱ隱槭ｒ菴ｿ逕ｨ縺励※縺・ｋ蝣ｴ蜷医・UICulture> 繧・en-US 縺ｫ險ｭ螳壹＠縺ｾ縺吶よｬ｡縺ｫ縲・
//荳九・ NeutralResourceLanguage 螻樊ｧ縺ｮ繧ｳ繝｡繝ｳ繝医ｒ隗｣髯､縺励∪縺吶ゆｸ九・陦後・ "en-US" 繧・
//繝励Ο繧ｸ繧ｧ繧ｯ繝・繝輔ぃ繧､繝ｫ縺ｮ UICulture 險ｭ螳壹→荳閾ｴ縺吶ｋ繧医≧譖ｴ譁ｰ縺励∪縺吶・

//[assembly: NeutralResourcesLanguage("en-US", UltimateResourceFallbackLocation.Satellite)]


[assembly: ThemeInfo(
    ResourceDictionaryLocation.None, //繝・・繝槫崋譛峨・繝ｪ繧ｽ繝ｼ繧ｹ 繝・ぅ繧ｯ繧ｷ繝ｧ繝翫Μ縺檎ｽｮ縺九ｌ縺ｦ縺・ｋ蝣ｴ謇
                                     //(繝ｪ繧ｽ繝ｼ繧ｹ縺後・繝ｼ繧ｸ縲・
                                     // 縺ｾ縺溘・繧｢繝励Μ繧ｱ繝ｼ繧ｷ繝ｧ繝ｳ 繝ｪ繧ｽ繝ｼ繧ｹ 繝・ぅ繧ｯ繧ｷ繝ｧ繝翫Μ縺ｫ隕九▽縺九ｉ縺ｪ縺・ｴ蜷医↓菴ｿ逕ｨ縺輔ｌ縺ｾ縺・
    ResourceDictionaryLocation.SourceAssembly //豎守畑繝ｪ繧ｽ繝ｼ繧ｹ 繝・ぅ繧ｯ繧ｷ繝ｧ繝翫Μ縺檎ｽｮ縺九ｌ縺ｦ縺・ｋ蝣ｴ謇
                                              //(繝ｪ繧ｽ繝ｼ繧ｹ縺後・繝ｼ繧ｸ縲・
                                              //繧｢繝励Μ繧ｱ繝ｼ繧ｷ繝ｧ繝ｳ縲√∪縺溘・縺・★繧後・繝・・繝槫崋譛峨・繝ｪ繧ｽ繝ｼ繧ｹ 繝・ぅ繧ｯ繧ｷ繝ｧ繝翫Μ縺ｫ繧りｦ九▽縺九ｉ縺ｪ縺・ｴ蜷医↓菴ｿ逕ｨ縺輔ｌ縺ｾ縺・
)]


// 繧｢繧ｻ繝ｳ繝悶Μ縺ｮ繝舌・繧ｸ繝ｧ繝ｳ諠・ｱ縺ｯ谺｡縺ｮ 4 縺､縺ｮ蛟､縺ｧ讒区・縺輔ｌ縺ｦ縺・∪縺・
//
//      繝｡繧ｸ繝｣繝ｼ 繝舌・繧ｸ繝ｧ繝ｳ
//      繝槭う繝翫・ 繝舌・繧ｸ繝ｧ繝ｳ
//      繝薙Ν繝臥分蜿ｷ
//      繝ｪ繝薙ず繝ｧ繝ｳ
//
// 縺吶∋縺ｦ縺ｮ蛟､繧呈欠螳壹☆繧九°縲∵ｬ｡繧剃ｽｿ逕ｨ縺励※繝薙Ν繝臥分蜿ｷ縺ｨ繝ｪ繝薙ず繝ｧ繝ｳ逡ｪ蜿ｷ繧呈里螳壹↓險ｭ螳壹〒縺阪∪縺・
// 譌｢螳壼､縺ｫ縺吶ｋ縺薙→縺後〒縺阪∪縺・
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("1.0.2.0")]
[assembly: AssemblyFileVersion("1.0.2.0")]
