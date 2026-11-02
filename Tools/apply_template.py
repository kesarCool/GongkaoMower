"""Apply optimized resume content into 模板.docx using direct paragraph mapping."""
from docx import Document
from docx.shared import Pt, RGBColor
from docx.oxml.ns import qn
from docx.oxml import OxmlElement
from copy import deepcopy

SRC = r"g:\Demo\GongkaoMower\模板.docx"
DST = r"g:\Demo\GongkaoMower\黄克勤个人简历_优化版.docx"

doc = Document(SRC)
body = doc.element.body
FONT = "微软雅黑"

# ── Helpers ──

def clear_para(p):
    for r in p.runs:
        r._element.getparent().remove(r._element)

def set_text(p, text, size_pt=11, bold=False, color='666666'):
    clear_para(p)
    run = p.add_run(text)
    run.font.name = FONT
    run.font.size = Pt(size_pt)
    run.bold = bold
    if color:
        run.font.color.rgb = RGBColor.from_string(color)
    rPr = run._r.get_or_add_rPr()
    rFonts = OxmlElement('w:rFonts')
    rFonts.set(qn('w:eastAsia'), FONT)
    rPr.insert(0, rFonts)

def set_bullet(p, label, desc):
    clear_para(p)
    r1 = p.add_run(label)
    r1.font.name = FONT; r1.font.size = Pt(11); r1.bold = True
    r1.font.color.rgb = RGBColor.from_string('666666')
    rPr1 = r1._r.get_or_add_rPr()
    rf1 = OxmlElement('w:rFonts'); rf1.set(qn('w:eastAsia'), FONT)
    rPr1.insert(0, rf1)

    r2 = p.add_run(": " + desc)
    r2.font.name = FONT; r2.font.size = Pt(11); r2.bold = False
    r2.font.color.rgb = RGBColor.from_string('666666')
    rPr2 = r2._r.get_or_add_rPr()
    rf2 = OxmlElement('w:rFonts'); rf2.set(qn('w:eastAsia'), FONT)
    rPr2.insert(0, rf2)

def dup_para_props(src_para, dst_para):
    """Copy paragraph-level properties from src to dst."""
    src_pPr = src_para._element.find(qn('w:pPr'))
    dst_pPr = dst_para._element.find(qn('w:pPr'))
    if src_pPr is not None and dst_pPr is not None:
        dst_para._element.remove(dst_pPr)
        dst_para._element.insert(0, deepcopy(src_pPr))

def insert_para_before(template_para, target_para):
    """Create a new paragraph formatted like template_para, inserted before target_para."""
    new_p = doc.add_paragraph()
    dup_para_props(template_para, new_p)
    target_para._element.addprevious(new_p._element)
    return new_p

# ═══════════════════════════════════════════════
# Step 1: Collect paragraph references (before any insertion)
# ═══════════════════════════════════════════════
all_p = list(doc.paragraphs)

# Verify indices
name_p       = all_p[1]   # 黄克勤 (white text, don't touch)
section_qz   = all_p[5]   # 求职意向
work_exp_p   = all_p[9]   # 工作经历 - insert point for 个人项目
zq_company   = all_p[12]  # 2020.9-2025.10 中青宝
zq_role      = all_p[13]  # 前端开发工程师 | 手机游戏
zq_empty     = all_p[14]  # empty after role
zq_bullets   = [all_p[15], all_p[16], all_p[17], all_p[18]]
cy_empty     = all_p[19]  # empty before 禅游
cy_company   = all_p[20]  # 2018.5-2020.8 禅游
cy_role      = all_p[21]  # 前端开发工程师 | 小游戏
cy_empty2    = all_p[22]  # empty after role
cy_bullets   = [all_p[23], all_p[24], all_p[25]]
section_edu  = all_p[27]  # 教育经历
# P28, P29: spacing
edu_line1_p  = all_p[30]  # 2014.9-2018.7
edu_line2_p  = all_p[31]  # 软件工程 | 本科
section_skill= all_p[33]  # 相关技能
skill_bullets= [all_p[36], all_p[37], all_p[38], all_p[39], all_p[40]]

print(f"Template structure verified: {len(all_p)} paragraphs")

# ═══════════════════════════════════════════════
# Step 2: Insert 个人项目 section before 工作经历
# ═══════════════════════════════════════════════
target = work_exp_p

# Insert in REVERSE order (each goes immediately before work_exp_p)
insertions = [
    # (template_para, text, size, bold, color)
    # Empty after bullets - use zq_empty as template
    (zq_empty, '', 11, False, '666666'),
    # 4 bullets (reversed)
    (zq_bullets[0], ("工具链与局外系统",
        "搭建了章节关卡选择、星级解锁、角色养成、词汇预览等局外系统，编写了14个编辑器工具（一键微信构建、Text→TMP批量转换、Tilemap像素生成、批量关卡编辑等），覆盖从开发到出包的常用环节")),
    (zq_bullets[0], ("性能与框架",
        "搭建UGUI弹窗框架（UIManager + UIPanelBase，模态栈 + 独立Overlay + TimeScale引用计数暂停），设计GameObjectPool对象池（借出登记 + 自动回收）与CombatTargetRegistry索敌注册表（替代FindObjectsWithTag全场景扫描），在WebGL内存受限环境下维持同屏数百敌人流畅运行")),
    (zq_bullets[0], ("技能与数据架构",
        "实现ScriptableObject驱动的Roguelike技能系统（8种主动技能 + 5种被动 + 6种英雄特质），采用FlatBuffers + Excel配表的数据驱动架构，技能/怪物/关卡/Boss AI全部由配置定义、运行时动态组装")),
    (zq_bullets[0], ("引擎与平台适配",
        "基于Unity + IL2CPP/WebGL构建，接入WX-WASM-SDK-V2完成微信小游戏工程化转换；设计分包策略（WASM代码分包 + Brotli压缩）将首包控制在4MB以内；处理了IL2CPP反射裁剪兼容、TMP中文字体渲染（7000+汉字SDF）等适配问题，借助AI辅助定位了非Dev Build下SDK框架注入失效的根因")),
    # Empty before bullets
    (zq_empty, '', 11, False, '666666'),
    # Role line
    (zq_role, "《文字割草机》—— Roguelike割草类微信小游戏，2026.06.25上架", 12, False, '374957'),
    # Company line
    (zq_company, "2026.05 - 2026.06\t个人独立开发", 12, True, '374957'),
    # 2 empty spacing before company
    (work_exp_p, '', 11, False, '666666'),
    (work_exp_p, '', 11, False, '666666'),
    # Section header: 个人项目
    (work_exp_p, "个人项目", 14, True, '374957'),
]

for template_para, *args in reversed(insertions):
    new_p = insert_para_before(template_para, target)
    if isinstance(args[0], tuple):
        # It's a bullet (label, desc)
        set_bullet(new_p, args[0][0], args[0][1])
    else:
        set_text(new_p, args[0], size_pt=args[1], bold=args[2], color=args[3])

print("✅ Inserted 个人项目 section")

# ═══════════════════════════════════════════════
# Step 3: Update existing paragraphs in-place
# ═══════════════════════════════════════════════

# -- 中青宝 bullets --
zq_new = [
    ("Unity游戏客户端开发",
     "主导塔防肉鸽手游《守住那座塔》核心功能开发，基于UGUI + YooAsset + HybridCLR搭建项目框架，实现战斗寻敌、碰撞检测、粒子特效与对象池复用等核心系统，上线首月新增用户20万，热更新响应效率提升60%"),
    ("Cocos Creator技术栈落地",
     "负责三国SLG手游《御剑三国》客户端架构，采用Cocos Creator + FGUI + ProtoBuf技术栈，完成资源合图批处理、模块化拆分与海外多语言适配，支撑10+语言区域同步上线"),
    ("多渠道版本交付",
     "主导多平台SDK对接，完成安卓/iOS登录、支付、广告SDK接入，负责H5/Steam/QQ大厅/360等多渠道适配上线，累计覆盖用户超500万"),
    ("版本迭代与运维",
     "负责APK/IPA出包全流程，落地资源压缩加密与差分热更新机制，紧急Bug修复24小时内热更推送，线上问题解决率100%"),
]
for p, (label, desc) in zip(zq_bullets, zq_new):
    set_bullet(p, label, desc)

# -- 禅游 bullets --
cy_new = [
    ("棋牌类游戏开发",
     "独立负责《湖南跑得快》《天天爱双扣》等多款棋牌游戏客户端开发，完成UI交互逻辑、前后端数据联调，封装排行榜、商店、转盘等通用功能预制件，团队开发效率提升40%"),
    ("性能与适配优化",
     "落地资源分包加载、开放域资源处理方案，完成全机型适配与广告SDK接入，实现用户操作数据全链路埋点上报，包体压缩30%，主流机型适配率达99%"),
    ("多平台版本运维",
     "负责微信小程序、OPPO/VIVO/华为/小米快游戏等渠道版本更新与提审，累计完成20+版本迭代，渠道审核通过率100%"),
]
for p, (label, desc) in zip(cy_bullets, cy_new):
    set_bullet(p, label, desc)

# -- 技能 bullets (6 lines, was 5 -> need to add 1) --
skills_new = [
    ("引擎与框架", "Unity（UGUI、HybridCLR、YooAsset、IL2CPP/WebGL）、Cocos Creator（FGUI）"),
    ("编程语言", "C#、TypeScript、JavaScript"),
    ("平台与渠道", "微信小游戏、安卓/iOS、H5、Steam/PC、快游戏（OPPO/VIVO/华为/小米）"),
    ("工程化能力", "热更新（HybridCLR/差分）、资源管理（YooAsset/分包/Brotli压缩）、性能优化、包体优化、线上问题定位"),
    ("数据与协议", "ProtoBuf、FlatBuffers"),
    ("AI辅助开发", "Cursor、Claude Code、ChatGPT，用于代码生成、架构设计、Debug诊断与文档撰写"),
]
for i, (label, desc) in enumerate(skills_new):
    if i < len(skill_bullets):
        set_bullet(skill_bullets[i], label, desc)
    else:
        # Add extra bullet after the last existing one
        last = skill_bullets[-1]
        new_p = doc.add_paragraph()
        dup_para_props(last, new_p)
        # Re-position: move before the tables (which are after paragraphs)
        # add_paragraph puts it at end of body, which is after all old paragraphs but before tables
        # Actually, need to move it after the last skill bullet
        last_elem = last._element
        last_elem.addnext(new_p._element)
        set_bullet(new_p, label, desc)

print("✅ Updated all existing sections")

# ═══════════════════════════════════════════════
# Save
# ═══════════════════════════════════════════════
doc.save(DST)
print(f"✅ Saved to {DST}")
