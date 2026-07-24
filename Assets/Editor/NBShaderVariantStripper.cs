using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

/// <summary>
/// NBShader 构建期变体剔除。
///
/// 背景：NBShader 是 UberEffectShader，单 shader 声明了 1200 亿+ 变体组合空间，
/// 但本项目实际只用 56 个变体。不做剔除时 build 产物里 sharedassets6 会膨胀到 72MB
/// （其中 61MB 是变体空洞零填充），整体包体从 ~90MB 暴增到 154MB。
///
/// 本脚本在 BuildPipeline 编译 shader 变体前拦截，丢弃项目未使用的重型功能变体。
/// 采用「keyword 黑名单」策略：保守地砍掉确认没用的功能 keyword，保留基础渲染路径，
/// 既削减绝大部分变体，又不锁死未来新增特效的能力。
///
/// 验证方法：build 日志里搜 [NBShaderStripper]，会看到每个 pass 砍掉了多少变体。
/// 出现画面异常时，把对应 keyword 从 Blacklist 里移除即可恢复。
/// </summary>
public class NBShaderVariantStripper : IPreprocessShaders
{
    // NBShader 的 shader 资产路径（AssetDatabase.GetAssetPath 的结果）
    // 两个 shader 共享几乎相同的 keyword 体系，统一处理。
    static readonly HashSet<string> NBShaderPaths = new HashSet<string>
    {
        "Packages/com.xuanxuan.nb.fx/NBShaders/Shader/ParticleBase.shader",   // Effects/NBShader
        "Packages/com.xuanxuan.nb.fx/NBShaders2/Shader/NBShader.shader",       // Hidden/Effects/NBShader2开发中
    };

    /// <summary>
    /// 项目确认未使用的功能 keyword 黑名单。
    /// 含这些 keyword 的变体组合一律丢弃。
    /// 新增特效用到某个功能时，把对应 keyword 从这里删掉。
    /// 判断依据：本项目 2 个 NB 材质实际只启用 _FX_LIGHT_MODE_UNLIT / _CUSTOMDATA / _FLIPBOOKBLENDING。
    /// </summary>
    static readonly HashSet<string> Blacklist = new HashSet<string>
    {
        // —— 重型视觉功能（项目当前未用）——
        "_DISSOLVE",              // 溶解效果
        "_DISSOLVE_EDITOR_TEST",  // 溶解编辑器预览（无论如何都不该进 build）
        "_DISTORT_REFRACTION",    // 扭曲折射
        "_SCREEN_DISTORT_MODE",   // 屏幕扭曲
        "_DEPTH_DECAL",           // 深度贴花
        "_PARALLAX_MAPPING",      // 视差映射
        "_PARCUSTOMDATA_ON",      // 视差自定义数据

        // —— 调试变体（绝不该进发布包）——
        "NB_DEBUG_MASK",
        "NB_DEBUG_PNOISE",
        "NB_DEBUG_DISSOLVE",
        "NB_DEBUG_DISTORT",
        "NB_DEBUG_FRESNEL",
    };

    /// <summary>
    /// 进阶光照模式：项目目前只用 UNLIT（不透明无光照基础渲染）。
    /// 这些是 shader_feature 的多选一分支，启用后会乘出大量变体。
    /// 如果未来要做受光粒子，从白名单（即「允许保留」）里加入对应模式。
    /// </summary>
    static readonly HashSet<string> BlacklistLightModes = new HashSet<string>
    {
        "_FX_LIGHT_MODE_BLINN_PHONG",
        "_FX_LIGHT_MODE_HALF_LAMBERT",
        "_FX_LIGHT_MODE_PBR",
        "_FX_LIGHT_MODE_SIX_WAY",
        "VFX_SIX_WAY_ABSORPTION",
    };

    public int callbackOrder => 100;

    public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
    {
        string path = AssetDatabase.GetAssetPath(shader);
        if (!NBShaderPaths.Contains(path)) return;  // 非 NBShader 不处理

        int before = data.Count;

        for (int i = data.Count - 1; i >= 0; i--)
        {
            if (ShouldStrip(data[i]))
            {
                data.RemoveAt(i);
            }
        }

        int removed = before - data.Count;
        if (removed > 0)
        {
            Debug.Log($"[NBShaderStripper] {shader.name} / {snippet.passType} / {snippet.shaderType}: 剔除 {removed} 变体（{before} -> {data.Count}）");
        }
    }

    static bool ShouldStrip(ShaderCompilerData data)
    {
        var keywords = data.shaderKeywordSet.GetShaderKeywords();
        foreach (var kw in keywords)
        {
            string name = kw.GetKeywordName();

            if (Blacklist.Contains(name)) return true;
            if (BlacklistLightModes.Contains(name)) return true;
        }
        return false;
    }
}
