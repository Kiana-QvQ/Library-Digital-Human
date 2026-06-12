"""
将上传文档解析为角色/章节/对话并写入 Neo4j，供 KnowledgeRetriever 图查询使用。

解析规则（启发式）：
- 章节：行首匹配「第X章」「Chapter N」或「## 标题」；否则整份文档视为一章。
- 对话：行首为「角色名：」或「角色名:」的视为该角色的一条对话。
- 若文档路径命中 Config.NEO4J_SINGLE_CHAPTER_PATH_MARKERS（默认含「崩坏3」），则强制一文件一章，
  章节标题用文件名（去扩展名），避免正文中的标题行再拆出多个 Chapter。
"""

from __future__ import annotations

import logging
import os
import re
from dataclasses import dataclass
from typing import List, Optional, Tuple

from app.shared.config import Config

logger = logging.getLogger(__name__)


# 章节标题：第X章 / Chapter N / ## 标题
RE_CHAPTER_CN = re.compile(r"^第[一二三四五六七八九十百千\d]+章\s*(.*)$")
RE_CHAPTER_EN = re.compile(r"^Chapter\s*(\d+)\s*(.*)$", re.I)
RE_CHAPTER_MD = re.compile(r"^#+\s+(.+)$")

# 对话行：角色名 + 中文/英文冒号，限制角色名长度避免误匹配
RE_DIALOGUE = re.compile(r"^([^\s:：]{1,20})[：:]\s*(.*)$")


def use_single_chapter_per_file(file_path: Optional[str], doc_name: str) -> bool:
    """
    满足任一条件时 Neo4j 侧一文件只建一个 Chapter：
    - 路径中包含 NEO4J_SINGLE_CHAPTER_PATH_MARKERS（如 data/崩坏3/...）；
    - 原始文件名匹配 NEO4J_SINGLE_CHAPTER_FILENAME_REGEX（默认 ^\\[P\\d+\\]，对应 [P01]… 上传名）。
    """
    if file_path:
        norm = file_path.replace("\\", "/")
        if any(marker in norm for marker in Config.NEO4J_SINGLE_CHAPTER_PATH_MARKERS):
            return True
    pat = Config.NEO4J_SINGLE_CHAPTER_FILENAME_REGEX
    if pat and doc_name:
        try:
            if re.match(pat, doc_name.strip()):
                return True
        except re.error:
            logger.warning("NEO4J_SINGLE_CHAPTER_FILENAME_REGEX 无效，已忽略: %s", pat)
    return False


@dataclass
class ParsedChapter:
    id: str
    name: str
    number: int
    filename: str
    source_doc_id: str
    kb_id: str  # 所属知识库，用于在 Neo4j 中按 KB 隔离


@dataclass
class ParsedDialogue:
    id: str
    text: str
    character_name: str
    chapter_id: str
    start_line: int
    end_line: int
    source_doc_id: str
    kb_id: str  # 所属知识库，用于在 Neo4j 中按 KB 隔离


def parse_text_to_graph(
    text: str,
    doc_id: str,
    doc_name: str,
    kb_id: str,
    single_chapter_per_file: bool = False,
) -> Tuple[List[ParsedChapter], List[ParsedDialogue]]:
    """
    将正文解析为章节与对话列表。
    返回 (chapters, dialogues)，与 KnowledgeRetriever 使用的 schema 对应。

    single_chapter_per_file:
        True 时仅保留一个 Chapter（id={doc_id}_ch0），name 为文件名去扩展名；只解析对话行。
    """
    chapters: List[ParsedChapter] = []
    dialogues: List[ParsedDialogue] = []

    lines = text.split("\n")
    current_chapter_id = f"{doc_id}_ch0"
    display_name = (os.path.splitext(doc_name)[0] or doc_name).strip() if doc_name else "正文"
    current_chapter_name = display_name if single_chapter_per_file else (doc_name or "正文")
    current_chapter_number = 1
    current_chapter_filename = doc_name or "document"
    line_no = 0
    dialogue_idx = 0

    # 默认先建一章（整份文档）
    chapters.append(
        ParsedChapter(
            id=current_chapter_id,
            name=current_chapter_name,
            number=current_chapter_number,
            filename=current_chapter_filename,
            source_doc_id=doc_id,
            kb_id=kb_id,
        )
    )

    for i, raw in enumerate(lines):
        line_no = i + 1
        line = raw.strip()
        if not line:
            continue

        if not single_chapter_per_file:
            # 1) 识别章节标题
            m_cn = RE_CHAPTER_CN.match(line)
            m_en = RE_CHAPTER_EN.match(line)
            m_md = RE_CHAPTER_MD.match(line)
        else:
            m_cn = m_en = m_md = None

        if m_cn:
            title = (m_cn.group(1) or "").strip() or line
            current_chapter_number = len(chapters) + 1
            current_chapter_id = f"{doc_id}_ch{current_chapter_number - 1}"
            current_chapter_name = title
            chapters.append(
                ParsedChapter(
                    id=current_chapter_id,
                    name=current_chapter_name,
                    number=current_chapter_number,
                    filename=current_chapter_filename,
                    source_doc_id=doc_id,
                    kb_id=kb_id,
                )
            )
            continue
        if m_en:
            num = m_en.group(1)
            title = (m_en.group(2) or "").strip() or f"Chapter {num}"
            current_chapter_number = int(num) if num.isdigit() else (len(chapters) + 1)
            current_chapter_id = f"{doc_id}_ch{current_chapter_number}"
            current_chapter_name = title
            chapters.append(
                ParsedChapter(
                    id=current_chapter_id,
                    name=current_chapter_name,
                    number=current_chapter_number,
                    filename=current_chapter_filename,
                    source_doc_id=doc_id,
                    kb_id=kb_id,
                )
            )
            continue
        if m_md and line.startswith("##"):
            title = (m_md.group(1) or "").strip() or line
            current_chapter_number = len(chapters) + 1
            current_chapter_id = f"{doc_id}_ch{current_chapter_number - 1}"
            current_chapter_name = title
            chapters.append(
                ParsedChapter(
                    id=current_chapter_id,
                    name=current_chapter_name,
                    number=current_chapter_number,
                    filename=current_chapter_filename,
                    source_doc_id=doc_id,
                    kb_id=kb_id,
                )
            )
            continue

        # 2) 识别对话行：角色名：内容
        m_d = RE_DIALOGUE.match(line)
        if m_d:
            char_name = (m_d.group(1) or "").strip()
            content = (m_d.group(2) or "").strip()
            if not char_name or not content:
                continue
            dialogue_idx += 1
            dialogue_id = f"{doc_id}_d{dialogue_idx}"
            dialogues.append(
                ParsedDialogue(
                    id=dialogue_id,
                    text=content,
                    character_name=char_name,
                    chapter_id=current_chapter_id,
                    start_line=line_no,
                    end_line=line_no,
                    source_doc_id=doc_id,
                    kb_id=kb_id,
                )
            )

    return chapters, dialogues


def ingest_document_to_neo4j(
    text: str,
    doc_id: str,
    doc_name: str,
    kb_id: str,
    graph_db=None,
    file_path: Optional[str] = None,
) -> bool:
    """
    将文档正文解析后写入 Neo4j（Character / Chapter / Dialogue 及关系）。
    若 graph_db 为 None 或未连接，则跳过并返回 False；失败时打日志并返回 False。
    """
    if graph_db is None or not getattr(graph_db, "driver", None):
        logger.debug("Neo4j 未连接，跳过图写入")
        return False

    single_ch = use_single_chapter_per_file(file_path, doc_name)
    try:
        chapters, dialogues = parse_text_to_graph(
            text,
            doc_id,
            doc_name,
            kb_id,
            single_chapter_per_file=single_ch,
        )
    except Exception as e:
        logger.warning("解析文档为图结构失败: %s", e)
        return False

    if not chapters and not dialogues:
        logger.debug("文档未解析出章节或对话，跳过 Neo4j 写入")
        return True

    try:
        with graph_db.driver.session() as session:
            # 删除本文档已有的章节与对话（避免重复解析堆积）
            session.run(
                "MATCH (d:Dialogue {source_doc_id: $doc_id, kb_id: $kb_id}) DETACH DELETE d",
                {"doc_id": doc_id, "kb_id": kb_id},
            )
            session.run(
                "MATCH (ch:Chapter {source_doc_id: $doc_id, kb_id: $kb_id}) DETACH DELETE ch",
                {"doc_id": doc_id, "kb_id": kb_id},
            )

            # 写入章节
            for ch in chapters:
                session.run(
                    """
                    MERGE (ch:Chapter {id: $id})
                    SET ch.name = $name,
                        ch.number = $number,
                        ch.filename = $filename,
                        ch.source_doc_id = $source_doc_id,
                        ch.kb_id = $kb_id
                    """,
                    {
                        "id": ch.id,
                        "name": ch.name,
                        "number": ch.number,
                        "filename": ch.filename,
                        "source_doc_id": ch.source_doc_id,
                        "kb_id": ch.kb_id,
                    },
                )

            # 写入角色与对话及关系（按对话顺序，保证 SPEAKS / BELONGS_TO）
            for d in dialogues:
                # Character 按 (name, kb_id) 隔离，避免不同知识库的同名角色混在一起
                session.run(
                    "MERGE (c:Character {name: $name, kb_id: $kb_id})",
                    {"name": d.character_name, "kb_id": d.kb_id},
                )
                session.run(
                    """
                    MERGE (d:Dialogue {id: $id})
                    SET d.text = $text,
                        d.source_doc_id = $source_doc_id,
                        d.kb_id = $kb_id
                    """,
                    {
                        "id": d.id,
                        "text": d.text,
                        "source_doc_id": d.source_doc_id,
                        "kb_id": d.kb_id,
                    },
                )
                session.run(
                    """
                    MATCH (c:Character {name: $char_name, kb_id: $kb_id})
                    MATCH (d:Dialogue {id: $dialogue_id})
                    MERGE (c)-[:SPEAKS]->(d)
                    """,
                    {
                        "char_name": d.character_name,
                        "dialogue_id": d.id,
                        "kb_id": d.kb_id,
                    },
                )
                session.run(
                    """
                    MATCH (d:Dialogue {id: $dialogue_id})
                    MATCH (ch:Chapter {id: $chapter_id})
                    MERGE (d)-[:BELONGS_TO {start_line: $start_line, end_line: $end_line}]->(ch)
                    """,
                    {
                        "dialogue_id": d.id,
                        "chapter_id": d.chapter_id,
                        "start_line": d.start_line,
                        "end_line": d.end_line,
                    },
                )
            # 角色出现在章节：本章出现过的角色 -[:APPEARS_IN]-> 本章
            chapter_ids_in_doc = {ch.id for ch in chapters}
            for d in dialogues:
                if d.chapter_id not in chapter_ids_in_doc:
                    continue
                session.run(
                    """
                    MATCH (c:Character {name: $char_name, kb_id: $kb_id})
                    MATCH (ch:Chapter {id: $chapter_id})
                    MERGE (c)-[:APPEARS_IN]->(ch)
                    """,
                    {
                        "char_name": d.character_name,
                        "chapter_id": d.chapter_id,
                        "kb_id": d.kb_id,
                    },
                )

            # 简单 SPEAKS_TO：相邻两条对话若角色不同，则前一个角色 -> 后一个角色（同章）
            for i in range(len(dialogues) - 1):
                a, b = dialogues[i], dialogues[i + 1]
                if a.character_name != b.character_name and a.chapter_id == b.chapter_id:
                    session.run(
                        """
                        MATCH (c1:Character {name: $n1})
                        MATCH (c2:Character {name: $n2})
                        MERGE (c1)-[r:SPEAKS_TO]->(c2)
                        ON CREATE SET r.chapter = $chapter_id
                        """,
                        {"n1": a.character_name, "n2": b.character_name, "chapter_id": a.chapter_id},
                    )

        logger.info(
            "Neo4j 图写入完成: doc_id=%s, chapters=%d, dialogues=%d",
            doc_id,
            len(chapters),
            len(dialogues),
        )
        return True
    except Exception as e:
        logger.warning("写入 Neo4j 失败（不影响向量化）: %s", e)
        return False


def ingest_chat_turn_to_neo4j(
    session_id: str,
    user_id: Optional[str],
    user_message: str,
    assistant_message: str,
    turn_index: int,
    graph_db=None,
) -> bool:
    """
    将用户与助手的一轮对话写入 Neo4j（复用 Character/Dialogue/Chapter 结构）。
    - 会话对应一章 Chapter(id=chat_session_<session_id>)，角色为「用户」「助手」。
    - 每轮两条 Dialogue（用户一句、助手一句），关系 SPEAKS / BELONGS_TO / APPEARS_IN。
    若 graph_db 为 None 或未连接，则跳过并返回 False。
    """
    if graph_db is None or not getattr(graph_db, "driver", None):
        return False

    if not (user_message or assistant_message):
        return True

    chapter_id = f"chat_session_{session_id}"
    uid = f"chat_{session_id}_u_{turn_index}"
    aid = f"chat_{session_id}_a_{turn_index}"

    try:
        with graph_db.driver.session() as session:
            # 会话章节（不存在则创建）
            session.run(
                """
                MERGE (ch:Chapter {id: $id})
                SET ch.name = $name, ch.number = 0, ch.filename = $filename, ch.source_session_id = $source_session_id
                """,
                {
                    "id": chapter_id,
                    "name": "用户会话",
                    "filename": session_id,
                    "source_session_id": session_id,
                },
            )
            # 角色
            session.run("MERGE (c:Character {name: $name})", {"name": "用户"})
            session.run("MERGE (c:Character {name: $name})", {"name": "助手"})
            # 用户对话节点
            session.run(
                """
                MERGE (d:Dialogue {id: $id})
                SET d.text = $text, d.source_session_id = $source_session_id
                """,
                {"id": uid, "text": (user_message or "").strip(), "source_session_id": session_id},
            )
            session.run(
                """
                MATCH (c:Character {name: '用户'})
                MATCH (d:Dialogue {id: $dialogue_id})
                MERGE (c)-[:SPEAKS]->(d)
                """,
                {"dialogue_id": uid},
            )
            session.run(
                """
                MATCH (d:Dialogue {id: $dialogue_id})
                MATCH (ch:Chapter {id: $chapter_id})
                MERGE (d)-[:BELONGS_TO {start_line: $turn, end_line: $turn}]->(ch)
                """,
                {"dialogue_id": uid, "chapter_id": chapter_id, "turn": turn_index},
            )
            session.run(
                """
                MATCH (c:Character {name: '用户'})
                MATCH (ch:Chapter {id: $chapter_id})
                MERGE (c)-[:APPEARS_IN]->(ch)
                """,
                {"chapter_id": chapter_id},
            )
            # 助手对话节点
            session.run(
                """
                MERGE (d:Dialogue {id: $id})
                SET d.text = $text, d.source_session_id = $source_session_id
                """,
                {"id": aid, "text": (assistant_message or "").strip(), "source_session_id": session_id},
            )
            session.run(
                """
                MATCH (c:Character {name: '助手'})
                MATCH (d:Dialogue {id: $dialogue_id})
                MERGE (c)-[:SPEAKS]->(d)
                """,
                {"dialogue_id": aid},
            )
            session.run(
                """
                MATCH (d:Dialogue {id: $dialogue_id})
                MATCH (ch:Chapter {id: $chapter_id})
                MERGE (d)-[:BELONGS_TO {start_line: $turn, end_line: $turn}]->(ch)
                """,
                {"dialogue_id": aid, "chapter_id": chapter_id, "turn": turn_index},
            )
            session.run(
                """
                MATCH (c:Character {name: '助手'})
                MATCH (ch:Chapter {id: $chapter_id})
                MERGE (c)-[:APPEARS_IN]->(ch)
                """,
                {"chapter_id": chapter_id},
            )
            # 用户 -> 助手 的 SPEAKS_TO（本轮）
            session.run(
                """
                MATCH (c1:Character {name: '用户'})
                MATCH (c2:Character {name: '助手'})
                MERGE (c1)-[r:SPEAKS_TO]->(c2)
                ON CREATE SET r.chapter = $chapter_id
                """,
                {"chapter_id": chapter_id},
            )
        logger.debug("Neo4j 已写入用户对话: session_id=%s turn=%s", session_id, turn_index)
        return True
    except Exception as e:
        logger.warning("写入 Neo4j 用户对话失败: %s", e)
        return False


def delete_document_from_neo4j(doc_id: str, kb_id: str) -> bool:
    """
    删除指定文档在 Neo4j 中写入的 Chapter / Dialogue（与 ingest 写入前清理逻辑一致）。
    Character 节点保留（可能被其他文档复用）。
    """
    graph_db = get_neo4j_graph_db_optional()
    if not graph_db:
        return True
    try:
        with graph_db.driver.session() as session:
            session.run(
                "MATCH (d:Dialogue {source_doc_id: $doc_id, kb_id: $kb_id}) DETACH DELETE d",
                {"doc_id": doc_id, "kb_id": kb_id},
            )
            session.run(
                "MATCH (ch:Chapter {source_doc_id: $doc_id, kb_id: $kb_id}) DETACH DELETE ch",
                {"doc_id": doc_id, "kb_id": kb_id},
            )
        logger.info("Neo4j 已删除文档图数据: doc_id=%s kb_id=%s", doc_id, kb_id)
        return True
    except Exception as e:
        logger.warning("Neo4j 删除文档图数据失败: %s", e)
        return False
    finally:
        graph_db.close()


def get_neo4j_graph_db_optional():
    """若 Neo4j 可用则返回 Neo4jGraphDB 实例，否则返回 None。用于 ingest 时可选写入。"""
    try:
        from app.brain.memory.neo4j_graph_db import Neo4jGraphDB

        db = Neo4jGraphDB(
            uri=Config.NEO4J_URI,
            username=Config.NEO4J_USER,
            password=Config.NEO4J_PASSWORD,
            auto_start=False,
            max_retries=0,
        )
        if db.driver:
            return db
    except Exception as e:
        logger.debug("Neo4j 不可用: %s", e)
    return None
