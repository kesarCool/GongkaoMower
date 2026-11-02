"""Rebuild 黄克勤个人简历.docx with optimized content."""
from docx import Document
from docx.shared import Pt
from docx.oxml.ns import qn
from docx.oxml import OxmlElement
from copy import deepcopy

SRC = r"g:\Demo\GongkaoMower\黄克勤个人简历.docx"
DST = SRC.replace(".docx", "_优化版.docx")  # avoid lock if file open in Word

doc = Document(SRC)

# ── save tables before clearing body ──
saved_tables = []
for table in doc.tables:
    saved_tables.append(deepcopy(table._tbl))

# ── clear body (preserves sectPr) ──
body = doc.element.body
for child in list(body):
    body.remove(child)

# ── helpers ──
FONT = "微软雅黑"

def _set_font(run, size_pt, bold=False, color=None):
    run.font.name = FONT
    run.font.size = Pt(size_pt)
    run.bold = bold
    if color:
        run.font.color.rgb = color
    # east-asian font fallback
    rPr = run._r.get_or_add_rPr()
    rFonts = rPr.find(qn('w:rFonts'))
    if rFonts is None:
        rFonts = OxmlElement('w:rFonts')
        rPr.insert(0, rFonts)
    rFonts.set(qn('w:eastAsia'), FONT)

def add_empty():
    p = doc.add_paragraph()
    return p

def add_section_header(text):
    p = doc.add_paragraph()
    r = p.add_run(text)
    _set_font(r, 14, bold=True)
    return p

def add_company_line(text):
    p = doc.add_paragraph()
    r = p.add_run(text)
    _set_font(r, 12, bold=True)
    return p

def add_role_line(text):
    p = doc.add_paragraph()
    r = p.add_run(text)
    _set_font(r, 12, bold=False)
    return p

def add_bullet(label, text):
    """Bold label + ': ' + regular description, 11pt."""
    p = doc.add_paragraph()
    r1 = p.add_run(label)
    _set_font(r1, 11, bold=True)
    r2 = p.add_run(": " + text)
    _set_font(r2, 11, bold=False)
    return p

def add_skill_line(label, text):
    """Bold label + ': ' + regular text, 11pt (same format as bullets)."""
    return add_bullet(label, text)


# ═══════════════════════════════════════════════
# 1. Name
# ═══════════════════════════════════════════════
add_empty()
p = doc.add_paragraph()
r = p.add_run("黄克勤")
_set_font(r, 26, bold=False)
add_empty()
add_empty()
add_empty()

# ═══════════════════════════════════════════════
# 2. 求职意向
# ═══════════════════════════════════════════════
add_section_header("求职意向")
add_empty()
add_empty()
add_empty()

# ═══════════════════════════════════════════════
# 3. 个人项目
# ═══════════════════════════════════════════════
add_section_header("个人项目")
add_empty()
add_empty()

add_company_line("2026.05 - 2026.06\t个人独立开发")
add_role_line("《文字割草机》—— Roguelike割草类微信小游戏，2026.06.25上架")
add_empty()

add_bullet(
    "引擎与平台适配",
    "基于Unity + IL2CPP/WebGL构建，接入WX-WASM-SDK-V2完成微信小游戏工程化转换；设计分包策略（WASM代码分包 + Brotli压缩）将首包控制在4MB以内；处理了IL2CPP反射裁剪兼容、TMP中文字体渲染（7000+汉字SDF）等适配问题，借助AI辅助定位了非Dev Build下SDK框架注入失效的根因"
)
add_bullet(
    "技能与数据架构",
    "实现ScriptableObject驱动的Roguelike技能系统（8种主动技能 + 5种被动 + 6种英雄特质），采用FlatBuffers + Excel配表的数据驱动架构，技能/怪物/关卡/Boss AI全部由配置定义、运行时动态组装"
)
add_bullet(
    "性能与框架",
    "搭建UGUI弹窗框架（UIManager + UIPanelBase，模态栈 + 独立Overlay + TimeScale引用计数暂停），设计GameObjectPool对象池（借出登记 + 自动回收）与CombatTargetRegistry索敌注册表（替代FindObjectsWithTag全场景扫描），在WebGL内存受限环境下维持同屏数百敌人流畅运行"
)
add_bullet(
    "工具链与局外系统",
    "搭建了章节关卡选择、星级解锁、角色养成、词汇预览等局外系统，编写了14个编辑器工具（一键微信构建、Text→TMP批量转换、Tilemap像素生成、批量关卡编辑等），覆盖从开发到出包的常用环节"
)
add_empty()

# ═══════════════════════════════════════════════
# 4. 工作经历
# ═══════════════════════════════════════════════
add_section_header("工作经历")
add_empty()
add_empty()

# -- 中青宝 --
add_company_line("2020.9 - 2025.10\t深圳中青宝互动网络股份有限公司")
add_role_line("前端开发工程师 | 手机游戏")
add_empty()

add_bullet(
    "Unity游戏客户端开发",
    "主导塔防肉鸽手游《守住那座塔》核心功能开发，基于UGUI + YooAsset + HybridCLR搭建项目框架，实现战斗寻敌、碰撞检测、粒子特效与对象池复用等核心系统，上线首月新增用户20万，热更新响应效率提升60%"
)
add_bullet(
    "Cocos Creator技术栈落地",
    "负责三国SLG手游《御剑三国》客户端架构，采用Cocos Creator + FGUI + ProtoBuf技术栈，完成资源合图批处理、模块化拆分与海外多语言适配，支撑10+语言区域同步上线"
)
add_bullet(
    "多渠道版本交付",
    "主导多平台SDK对接，完成安卓/iOS登录、支付、广告SDK接入，负责H5/Steam/QQ大厅/360等多渠道适配上线，累计覆盖用户超500万"
)
add_bullet(
    "版本迭代与运维",
    "负责APK/IPA出包全流程，落地资源压缩加密与差分热更新机制，紧急Bug修复24小时内热更推送，线上问题解决率100%"
)
add_empty()

# -- 禅游 --
add_company_line("2018.5 - 2020.8\t深圳市禅游科技股份有限公司")
add_role_line("前端开发工程师 | 小游戏")
add_empty()

add_bullet(
    "棋牌类游戏开发",
    "独立负责《湖南跑得快》《天天爱双扣》等多款棋牌游戏客户端开发，完成UI交互逻辑、前后端数据联调，封装排行榜、商店、转盘等通用功能预制件，团队开发效率提升40%"
)
add_bullet(
    "性能与适配优化",
    "落地资源分包加载、开放域资源处理方案，完成全机型适配与广告SDK接入，实现用户操作数据全链路埋点上报，包体压缩30%，主流机型适配率达99%"
)
add_bullet(
    "多平台版本运维",
    "负责微信小程序、OPPO/VIVO/华为/小米快游戏等渠道版本更新与提审，累计完成20+版本迭代，渠道审核通过率100%"
)
add_empty()

# ═══════════════════════════════════════════════
# 5. 教育经历
# ═══════════════════════════════════════════════
add_section_header("教育经历")
add_empty()
add_empty()

add_company_line("2014.9 - 2018.7\t吉首大学")
add_role_line("软件工程 | 本科")
add_empty()

# ═══════════════════════════════════════════════
# 6. 相关技能
# ═══════════════════════════════════════════════
add_section_header("相关技能")
add_empty()
add_empty()

add_skill_line(
    "引擎与框架",
    "Unity（UGUI、HybridCLR、YooAsset、IL2CPP/WebGL）、Cocos Creator（FGUI）"
)
add_skill_line(
    "编程语言",
    "C#、TypeScript、JavaScript"
)
add_skill_line(
    "平台与渠道",
    "微信小游戏、安卓/iOS、H5、Steam/PC、快游戏（OPPO/VIVO/华为/小米）"
)
add_skill_line(
    "工程化能力",
    "热更新（HybridCLR/差分）、资源管理（YooAsset/分包/Brotli压缩）、性能优化、包体优化、线上问题定位"
)
add_skill_line(
    "数据与协议",
    "ProtoBuf、FlatBuffers"
)
add_skill_line(
    "AI辅助开发",
    "Cursor、Claude Code、ChatGPT，用于代码生成、架构设计、Debug诊断与文档撰写"
)

# ═══════════════════════════════════════════════
# Append saved tables
# ═══════════════════════════════════════════════
for tbl_xml in saved_tables:
    body.append(tbl_xml)

# ── save ──
doc.save(DST)
print("✅ Resume rebuilt and saved.")
