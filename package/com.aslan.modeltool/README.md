# Aslan 3D Model Tool

Unity Editor uzerinde prompt tabanli 3D model uretimi yapar. Blender batch mode ile FBX olusturur, Unity import ayarlarini uygular, istenirse prefab olusturur.

## Menu
- Tools/Aslan 3D Model Tool/Open Tool
- Tools/Aslan 3D Model Tool/Open Settings

## Kurulum
1. Unity projenizde `Packages/manifest.json` icine asagidaki satiri ekleyin:
   `"com.aslan.modeltool": "https://github.com/<kullanici>/unity-3d-model-tool.git?path=package/com.aslan.modeltool"`
2. `Tools > Aslan 3D Model Tool > Open Settings` ile Blender yolunu dogrulayin.
3. `Open Tool` penceresinden prompt ve count girip `Generate And Import` ile uretim yapin.

## Not
- Varsayilan cikis klasorleri: `Assets/Art/Generated3D` ve `Assets/Prefabs/Generated`
- Blender kurulumu gerekli.
