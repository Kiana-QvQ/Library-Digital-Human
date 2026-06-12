# """
# Neo4j图数据库模块
# 用于存储和查询知识图谱关系
# """

# from neo4j import GraphDatabase
# from typing import List, Dict, Any, Optional, Tuple
# import logging
# import subprocess
# import time
# import platform
# import os
# import socket
# import urllib.request
# import urllib.error
# from app.shared.config import Config

# logger = logging.getLogger(__name__)


# def check_port_open(host="localhost", port=7687, timeout=2):
#     """
#     检查端口是否开放
    
#     Args:
#         host: 主机地址
#         port: 端口号
#         timeout: 超时时间（秒）
        
#     Returns:
#         bool: 端口是否开放
#     """
#     try:
#         sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
#         sock.settimeout(timeout)
#         result = sock.connect_ex((host, port))
#         sock.close()
#         return result == 0
#     except:
#         return False


# def check_neo4j_browser(port=7474, timeout=3):
#     """
#     检查Neo4j Browser是否可访问
    
#     Args:
#         port: Neo4j Browser端口（默认7474）
#         timeout: 超时时间（秒）
        
#     Returns:
#         bool: Browser是否可访问
#     """
#     try:
#         url = f"http://localhost:{port}"
#         response = urllib.request.urlopen(url, timeout=timeout)
#         return response.getcode() == 200
#     except:
#         return False


# def check_neo4j_running(uri="bolt://localhost:7688", username="neo4j", password="neo4jneo4j"):
#     """
#     检查Neo4j是否正在运行
    
#     Args:
#         uri: Neo4j URI
#         username: 用户名
#         password: 密码
        
#     Returns:
#         bool: 是否正在运行
#     """
#     # 先检查端口是否开放（更快）
#     if "://" in uri:
#         host_port = uri.split("://")[1]
#         if ":" in host_port:
#             host, port = host_port.split(":")
#             port = int(port)
#         else:
#             host = host_port
#             port = 7687
#     else:
#         host = "localhost"
#         port = 7687
    
#     if not check_port_open(host, port, timeout=2):
#         return False
    
#     # 端口开放，尝试连接数据库
#     try:
#         from neo4j import GraphDatabase
#         driver = GraphDatabase.driver(uri, auth=(username, password), connection_timeout=3)
#         with driver.session() as session:
#             session.run("RETURN 1")
#         driver.close()
#         return True
#     except:
#         return False


# def check_neo4j_installed():
#     """
#     检查Neo4j是否已安装
    
#     Returns:
#         tuple: (是否安装, 安装路径, 安装类型)
#     """
#     system = platform.system()
#     installed_paths = []
#     install_type = None
    
#     if system == "Windows":
#         # 检查Neo4j Desktop
#         desktop_paths = [
#             os.path.expanduser(r'~\AppData\Local\Programs\Neo4j Desktop'),
#             r'C:\Program Files\Neo4j Desktop',
#             r'C:\Program Files (x86)\Neo4j Desktop',
#         ]
        
#         # 检查独立安装
#         standalone_paths = [
#             r'C:\neo4j',
#             os.path.expanduser(r'~\neo4j'),
#         ]
        
#         for path in desktop_paths:
#             if os.path.exists(path):
#                 installed_paths.append(path)
#                 install_type = "Desktop"
#                 break
        
#         for path in standalone_paths:
#             if os.path.exists(path):
#                 installed_paths.append(path)
#                 if install_type is None:
#                     install_type = "Standalone"
#                 break
        
#         # 检查Windows服务
#         try:
#             result = subprocess.run(
#                 ['sc', 'query', 'Neo4j'],
#                 capture_output=True,
#                 text=True,
#                 timeout=5
#             )
#             if result.returncode == 0 and 'Neo4j' in result.stdout:
#                 install_type = "Service"
#                 installed_paths.append("Windows Service")
#         except:
#             pass
    
#     return len(installed_paths) > 0, installed_paths, install_type


# def start_neo4j_service(max_wait_time=30):
#     """
#     尝试启动Neo4j服务并等待其完全启动
#     使用 neo4j console 命令在后台启动
    
#     Args:
#         max_wait_time: 最大等待时间（秒）
        
#     Returns:
#         bool: 是否成功启动
#     """
#     logger.info("尝试启动Neo4j服务（使用neo4j console）...")
    
#     # 首先检查是否已经在运行
#     if check_neo4j_running(uri="bolt://localhost:7687", username="neo4j", password="neo4jneo4j"):
#         logger.info("Neo4j服务已在运行")
#         if check_neo4j_browser():
#             logger.info("Neo4j Browser可访问: http://localhost:7474")
#         return True
    
#     # 检查是否已安装
#     is_installed, paths, install_type = check_neo4j_installed()
    
#     if not is_installed:
#         logger.error("未检测到Neo4j安装")
#         logger.error("请先安装Neo4j:")
#         logger.error("  1. Neo4j Desktop: https://neo4j.com/download/")
#         logger.error("  2. 或独立版本: https://neo4j.com/download-center/#community")
#         return False
    
#     logger.info(f"检测到Neo4j安装类型: {install_type}, 路径: {paths}")
    
#     system = platform.system()
#     start_success = False
    
#     if system == "Windows":
#         # Windows系统：尝试多种方式启动
#         methods = []
        
#         # 方法1: 使用Windows服务（最可靠）
#         methods.append({
#             'name': 'Windows服务启动',
#             'commands': [
#                 ['net', 'start', 'Neo4j'],
#                 ['sc', 'start', 'Neo4j'],
#             ],
#             'check_paths': []
#         })
        
#         # 方法2: 查找并启动Neo4j Desktop数据库
#         if install_type == "Desktop":
#             # Neo4j Desktop的数据库路径通常在用户目录下
#             desktop_db_paths = [
#                 os.path.expanduser(r'~\AppData\Roaming\Neo4j Desktop\Application\neo4jDatabases'),
#                 os.path.expanduser(r'~\AppData\Local\Neo4j Desktop\Application\neo4jDatabases'),
#             ]
            
#             for db_base in desktop_db_paths:
#                 if os.path.exists(db_base):
#                     # 查找最新的数据库目录
#                     for db_dir in os.listdir(db_base):
#                         db_path = os.path.join(db_base, db_dir)
#                         if os.path.isdir(db_path):
#                             # 查找bin目录
#                             for root, dirs, files in os.walk(db_path):
#                                 if 'neo4j.bat' in files or 'neo4j-admin.bat' in files:
#                                     bin_dir = root
#                                     neo4j_bat = os.path.join(bin_dir, 'neo4j.bat')
#                                     if os.path.exists(neo4j_bat):
#                                         methods.append({
#                                             'name': f'Neo4j Desktop数据库启动 ({db_dir})',
#                                             'commands': [],
#                                             'check_paths': [neo4j_bat]
#                                         })
#                                         break
        
#         # 方法3: 使用neo4j console启动（后台运行）- 优先使用此方法
#         # 查找neo4j.bat或neo4j.cmd
#         neo4j_bin_paths = []
        
#         # 查找可能的Neo4j安装路径
#         search_paths = [
#             r'C:\Program Files\Neo4j Desktop',
#             r'C:\neo4j',
#             os.path.expanduser(r'~\AppData\Local\Programs\Neo4j Desktop'),
#             os.path.expanduser(r'~\neo4j'),
#             os.path.expanduser(r'~\AppData\Roaming\Neo4j Desktop'),
#         ]
        
#         for base_path in search_paths:
#             if os.path.exists(base_path):
#                 # 查找bin目录下的neo4j.bat或neo4j.cmd
#                 for root, dirs, files in os.walk(base_path):
#                     if 'bin' in root.lower() or root.endswith('bin'):
#                         for file in files:
#                             if file.lower() in ['neo4j.bat', 'neo4j.cmd', 'neo4j']:
#                                 full_path = os.path.join(root, file)
#                                 if full_path not in neo4j_bin_paths:
#                                     neo4j_bin_paths.append(full_path)
        
#         # 如果找到neo4j命令，优先使用console模式启动（后台）
#         if neo4j_bin_paths:
#             # 将console模式添加到方法列表的最前面（优先尝试）
#             for neo4j_cmd in neo4j_bin_paths[:3]:  # 只尝试前3个
#                 methods.insert(0, {  # 插入到最前面，优先尝试
#                     'name': f'Neo4j Console启动 ({os.path.basename(neo4j_cmd)})',
#                     'commands': [],
#                     'check_paths': [neo4j_cmd],
#                     'use_console': True  # 标记使用console模式
#                 })
        
#         # 方法4: 使用neo4j start（备用）
#         methods.append({
#             'name': 'Neo4j Start启动',
#             'commands': [
#                 ['neo4j', 'start'],
#                 ['neo4j.bat', 'start'],
#             ],
#             'check_paths': [
#                 r'C:\Program Files\Neo4j Desktop\neo4j-community\bin\neo4j.bat',
#                 r'C:\neo4j\bin\neo4j.bat',
#                 os.path.expanduser(r'~\AppData\Local\Programs\Neo4j Desktop\neo4j-community\bin\neo4j.bat'),
#             ],
#             'use_console': False
#         })
        
#         for method in methods:
#             logger.info(f"尝试方法: {method['name']}")
            
#             # 先尝试直接命令
#             for cmd in method['commands']:
#                 try:
#                     result = subprocess.run(
#                         cmd,
#                         capture_output=True,
#                         text=True,
#                         timeout=15,
#                         shell=True  # Windows需要shell=True
#                     )
#                     stdout_lower = result.stdout.lower() if result.stdout else ""
#                     stderr_lower = result.stderr.lower() if result.stderr else ""
#                     output = stdout_lower + stderr_lower
                    
#                     if (result.returncode == 0 or 
#                         'started' in output or 
#                         'running' in output or
#                         '已经启动' in output or
#                         'already running' in output):
#                         logger.info(f"Neo4j启动命令执行成功: {' '.join(cmd)}")
#                         start_success = True
#                         break
#                     else:
#                         logger.debug(f"命令输出: {result.stdout} {result.stderr}")
#                 except (subprocess.TimeoutExpired, FileNotFoundError, Exception) as e:
#                     logger.debug(f"命令执行失败: {e}")
#                     continue
#             if start_success:
#                 break
            
#             # 尝试使用完整路径
#             for path in method['check_paths']:
#                 if os.path.exists(path):
#                     try:
#                         logger.info(f"尝试启动: {path}")
                        
#                         # 检查是否使用console模式
#                         use_console = method.get('use_console', False)
                        
#                         if use_console:
#                             # 使用neo4j console在后台启动
#                             # Windows下使用START命令在后台运行
#                             if system == "Windows":
#                                 # 使用START命令在后台运行console
#                                 # /B 表示不创建新窗口，在后台运行
#                                 cmd_str = f'START /B "" "{path}" console'
#                                 try:
#                                     # 使用Popen在后台运行，不等待完成
#                                     process = subprocess.Popen(
#                                         cmd_str,
#                                         shell=True,
#                                         stdout=subprocess.PIPE,
#                                         stderr=subprocess.PIPE,
#                                         cwd=os.path.dirname(path),
#                                         creationflags=subprocess.CREATE_NO_WINDOW if hasattr(subprocess, 'CREATE_NO_WINDOW') else 0
#                                     )
#                                     logger.info(f"Neo4j console命令已在后台启动: {path}")
#                                     logger.info("等待服务启动（检查端口7687）...")
#                                     start_success = True
#                                     break
#                                 except Exception as e:
#                                     logger.debug(f"启动失败: {e}")
#                                     continue
#                             else:
#                                 # Linux/Mac使用nohup在后台运行
#                                 try:
#                                     process = subprocess.Popen(
#                                         [path, 'console'],
#                                         stdout=subprocess.PIPE,
#                                         stderr=subprocess.PIPE,
#                                         cwd=os.path.dirname(path),
#                                         start_new_session=True  # 创建新会话，脱离父进程
#                                     )
#                                     logger.info(f"Neo4j console命令已在后台启动: {path}")
#                                     logger.info("等待服务启动（检查端口7687）...")
#                                     start_success = True
#                                     break
#                                 except Exception as e:
#                                     logger.debug(f"启动失败: {e}")
#                                     continue
#                         else:
#                             # 使用start命令
#                             result = subprocess.run(
#                                 [path, 'start'],
#                                 capture_output=True,
#                                 text=True,
#                                 timeout=15,
#                                 cwd=os.path.dirname(path),
#                                 shell=True
#                             )
#                             stdout_lower = result.stdout.lower() if result.stdout else ""
#                             stderr_lower = result.stderr.lower() if result.stderr else ""
#                             output = stdout_lower + stderr_lower
                            
#                             if (result.returncode == 0 or 
#                                 'started' in output or 
#                                 'running' in output or
#                                 '启动' in output):
#                                 logger.info(f"Neo4j启动命令执行成功: {path}")
#                                 start_success = True
#                                 break
#                             else:
#                                 logger.debug(f"启动输出: {result.stdout} {result.stderr}")
#                     except Exception as e:
#                         logger.debug(f"启动失败: {e}")
#                         continue
#                 if start_success:
#                     break
        
#         # 如果还没成功，尝试直接使用neo4j console命令（从PATH中查找）
#         if not start_success:
#             logger.info("尝试使用neo4j console命令启动（从系统PATH）...")
#             try:
#                 # Windows下尝试直接运行neo4j console
#                 if system == "Windows":
#                     # 使用START命令在后台运行
#                     cmd_str = 'START /B "" neo4j console'
#                     process = subprocess.Popen(
#                         cmd_str,
#                         shell=True,
#                         stdout=subprocess.PIPE,
#                         stderr=subprocess.PIPE,
#                         creationflags=subprocess.CREATE_NO_WINDOW if hasattr(subprocess, 'CREATE_NO_WINDOW') else 0
#                     )
#                     logger.info("neo4j console命令已执行（后台运行）")
#                     logger.info("等待服务启动（检查端口7687）...")
#                     start_success = True
#                 else:
#                     # Linux/Mac
#                     process = subprocess.Popen(
#                         ['neo4j', 'console'],
#                         stdout=subprocess.PIPE,
#                         stderr=subprocess.PIPE,
#                         start_new_session=True
#                     )
#                     logger.info("neo4j console命令已执行（后台运行）")
#                     logger.info("等待服务启动（检查端口7687）...")
#                     start_success = True
#             except FileNotFoundError:
#                 logger.debug("neo4j命令未在PATH中找到")
#             except Exception as e:
#                 logger.debug(f"neo4j console命令执行失败: {e}")
    
#     elif system == "Linux" or system == "Darwin":  # Linux or Mac
#         # Linux/Mac: 使用neo4j命令
#         try:
#             result = subprocess.run(
#                 ['neo4j', 'start'],
#                 capture_output=True,
#                 text=True,
#                 timeout=10
#             )
#             if result.returncode == 0:
#                 logger.info("Neo4j启动成功")
#                 return True
#         except FileNotFoundError:
#             # 尝试使用完整路径
#             common_paths = [
#                 '/usr/local/neo4j/bin/neo4j',
#                 '/opt/neo4j/bin/neo4j',
#                 os.path.expanduser('~/neo4j/bin/neo4j'),
#             ]
#             for path in common_paths:
#                 if os.path.exists(path):
#                     try:
#                         result = subprocess.run(
#                             [path, 'start'],
#                             capture_output=True,
#                             text=True,
#                             timeout=10
#                         )
#                         if result.returncode == 0:
#                             logger.info(f"Neo4j启动成功: {path}")
#                             return True
#                     except Exception:
#                         continue
    
#     # 如果启动命令执行成功，等待服务完全启动
#     if start_success:
#         logger.info(f"等待Neo4j服务完全启动（最多{max_wait_time}秒）...")
#         start_time = time.time()
        
#         while time.time() - start_time < max_wait_time:
#             # 检查Bolt端口
#             if check_port_open("localhost", 7687, timeout=2):
#                 logger.info("Neo4j Bolt端口(7687)已开放")
#                 # 再等待一下让服务完全就绪
#                 time.sleep(3)
                
#                 # 检查数据库连接（使用默认密码neo4jneo4j）
#                 if check_neo4j_running(uri="bolt://localhost:7687", username="neo4j", password="neo4jneo4j"):
#                     logger.info("✓ Neo4j数据库连接成功")
#                     logger.info("  用户名: neo4j")
#                     logger.info("  密码: neo4jneo4j")
                    
#                     # 检查Browser
#                     if check_neo4j_browser():
#                         logger.info("✓ Neo4j Browser可访问: http://localhost:7474")
#                         logger.info("  可以在浏览器中打开Neo4j Browser进行可视化操作")
#                         logger.info("  登录信息: 用户名=neo4j, 密码=neo4jneo4j")
#                     else:
#                         logger.warning("Neo4j Browser可能还未完全启动，请稍后访问: http://localhost:7474")
#                         logger.info("  登录信息: 用户名=neo4j, 密码=neo4jneo4j")
                    
#                     return True
#                 else:
#                     logger.debug("端口已开放但数据库连接失败，继续等待...")
            
#             time.sleep(2)
#             elapsed = int(time.time() - start_time)
#             if elapsed % 5 == 0:
#                 logger.info(f"  等待中... ({elapsed}/{max_wait_time}秒)")
        
#         logger.warning("Neo4j启动超时，但可能仍在启动中")
#         logger.warning("请手动检查: http://localhost:7474")
#         return False
    
#     logger.warning("无法自动启动Neo4j")
#     logger.warning("=" * 60)
#     logger.warning("Neo4j启动指南:")
#     logger.warning("=" * 60)
#     logger.warning("如果使用Neo4j Desktop:")
#     logger.warning("  1. 打开Neo4j Desktop应用程序")
#     logger.warning("  2. 创建或选择一个数据库项目")
#     logger.warning("  3. 点击'Start'按钮启动数据库")
#     logger.warning("  4. 确保数据库状态显示为'Running'")
#     logger.warning("")
#     logger.warning("如果使用独立安装:")
#     logger.warning("  1. 打开命令行（以管理员身份）")
#     logger.warning("  2. 进入Neo4j安装目录的bin文件夹")
#     logger.warning("  3. 运行: neo4j.bat start")
#     logger.warning("")
#     logger.warning("如果使用Windows服务:")
#     logger.warning("  1. 打开服务管理器 (services.msc)")
#     logger.warning("  2. 找到Neo4j服务")
#     logger.warning("  3. 右键点击并选择'启动'")
#     logger.warning("")
#     logger.warning("检查Neo4j是否运行:")
#     logger.warning("  - 打开浏览器访问: http://localhost:7474")
#     logger.warning("  - 如果能打开Neo4j Browser，说明服务已启动")
#     logger.warning("=" * 60)
#     return False


# class Neo4jGraphDB:
#     """Neo4j图数据库管理器"""
    
#     def __init__(self, uri: str = "bolt://localhost:7687", 
#                  username: str = "neo4j", password: str = "neo4jneo4j",
#                  auto_start: bool = True, max_retries: int = 3):
#         """
#         初始化Neo4j连接
        
#         Args:
#             uri: Neo4j数据库URI
#             username: 用户名
#             password: 密码
#             auto_start: 是否在连接失败时自动尝试启动Neo4j服务
#             max_retries: 最大重试次数
#         """
#         self.uri = uri
#         self.username = username
#         self.password = password
#         self.auto_start = auto_start
#         self.max_retries = max_retries
#         self.driver = None
#         self._connect_with_retry()
    
#     def _connect_with_retry(self):
#         """建立数据库连接，失败时自动重试"""
#         self.driver = None
        
#         # 首先检查Neo4j是否已经在运行（使用配置的用户名密码）
#         if check_neo4j_running(self.uri, self.username, self.password):
#             logger.info("检测到Neo4j服务已在运行")
#             try:
#                 self.driver = GraphDatabase.driver(
#                     self.uri, 
#                     auth=(self.username, self.password),
#                     connection_timeout=5
#                 )
#                 with self.driver.session() as session:
#                     session.run("RETURN 1")
#                 logger.info("Neo4j连接建立成功")
#                 return
#             except Exception as e:
#                 logger.warning(f"虽然检测到服务运行，但连接失败: {e}")
#                 logger.warning("可能是用户名或密码错误")
#                 logger.warning(f"当前配置: 用户名={self.username}, 密码={'*' * len(self.password)}")
        
#         # 检查Neo4j是否已安装
#         is_installed, paths, install_type = check_neo4j_installed()
#         if not is_installed and self.auto_start:
#             logger.error("=" * 60)
#             logger.error("Neo4j未安装，无法自动启动")
#             logger.error("=" * 60)
#             logger.error("请先安装Neo4j:")
#             logger.error("  1. Neo4j Desktop (推荐): https://neo4j.com/download/")
#             logger.error("  2. 或独立版本: https://neo4j.com/download-center/#community")
#             logger.error("=" * 60)
#             return
        
#         for attempt in range(self.max_retries):
#             try:
#                 self.driver = GraphDatabase.driver(
#                     self.uri, 
#                     auth=(self.username, self.password),
#                     connection_timeout=5  # 5秒连接超时
#                 )
#                 # 测试连接
#                 with self.driver.session() as session:
#                     session.run("RETURN 1")
#                 logger.info("Neo4j连接建立成功")
#                 return
#             except Exception as e:
#                 error_msg = str(e)
#                 if attempt < self.max_retries - 1:
#                     logger.warning(f"Neo4j连接失败 (尝试 {attempt + 1}/{self.max_retries})")
#                     logger.debug(f"错误详情: {error_msg}")
                    
#                     if self.auto_start:
#                         logger.info("尝试启动Neo4j服务...")
#                         if start_neo4j_service():
#                             logger.info("等待Neo4j服务启动（10秒）...")
#                             time.sleep(10)  # 增加等待时间
#                         else:
#                             logger.warning("无法自动启动Neo4j服务，请手动启动")
#                             # 只尝试启动一次，避免重复尝试
#                             self.auto_start = False
#                             time.sleep(2)
#                     else:
#                         time.sleep(2)
#                 else:
#                     logger.error(f"Neo4j连接最终失败")
#                     logger.error(f"错误: {error_msg}")
#                     logger.error("")
#                     logger.error("请检查:")
#                     logger.error("  1. Neo4j服务是否已启动")
#                     logger.error("  2. 连接URI是否正确: " + self.uri)
#                     logger.error("  3. 用户名和密码是否正确")
#                     logger.error("  4. 防火墙是否阻止了连接")
#                     logger.error("  5. 尝试访问: http://localhost:7474 查看Neo4j Browser")
#                     self.driver = None
    
#     def _connect(self):
#         """建立数据库连接（已弃用，使用_connect_with_retry）"""
#         self._connect_with_retry()
    
#     def close(self):
#         """关闭数据库连接"""
#         if self.driver:
#             self.driver.close()
#             logger.info("Neo4j连接已关闭")
    
#     def execute_query(self, query: str, parameters: Dict = None) -> List[Dict]:
#         """
#         执行Cypher查询
        
#         Args:
#             query: Cypher查询语句
#             parameters: 查询参数
            
#         Returns:
#             List[Dict]: 查询结果
#         """
#         if not self.driver:
#             logger.error("数据库未连接")
#             return []
        
#         try:
#             with self.driver.session() as session:
#                 result = session.run(query, parameters or {})
#                 return [record.data() for record in result]
#         except Exception as e:
#             logger.error(f"查询执行失败: {e}")
#             return []
    
#     def create_node(self, label: str, properties: Dict) -> bool:
#         """
#         创建节点
        
#         Args:
#             label: 节点标签
#             properties: 节点属性
            
#         Returns:
#             bool: 创建是否成功
#         """
#         query = f"CREATE (n:{label} $properties) RETURN n"
#         result = self.execute_query(query, {"properties": properties})
#         return len(result) > 0
    
#     def merge_node(self, label: str, match_properties: Dict, set_properties: Dict = None) -> bool:
#         """
#         合并节点（如果不存在则创建，存在则更新）
        
#         Args:
#             label: 节点标签
#             match_properties: 用于匹配的属性（唯一标识）
#             set_properties: 要设置的属性
            
#         Returns:
#             bool: 操作是否成功
#         """
#         # 构建MERGE的匹配属性
#         match_props_list = [f"{k}: ${f'match_{k}'}" for k in match_properties.keys()]
#         match_props_str = "{" + ", ".join(match_props_list) + "}"
        
#         # 构建参数
#         params = {}
#         for k, v in match_properties.items():
#             params[f"match_{k}"] = v
        
#         # 构建SET语句
#         if set_properties:
#             set_statements = ", ".join([f"n.{k} = ${f'set_{k}'}" for k in set_properties.keys()])
#             for k, v in set_properties.items():
#                 params[f"set_{k}"] = v
            
#             query = f"""
#             MERGE (n:{label} {match_props_str})
#             ON CREATE SET {set_statements}
#             ON MATCH SET {set_statements}
#             RETURN n
#             """
#         else:
#             query = f"""
#             MERGE (n:{label} {match_props_str})
#             RETURN n
#             """
        
#         result = self.execute_query(query, params)
#         return len(result) > 0
    
#     def create_relationship(self, from_node: Tuple[str, Dict], 
#                           to_node: Tuple[str, Dict], 
#                           relationship_type: str, 
#                           properties: Dict = None) -> bool:
#         """
#         创建关系
        
#         Args:
#             from_node: 起始节点 (label, properties)
#             to_node: 目标节点 (label, properties)
#             relationship_type: 关系类型
#             properties: 关系属性
            
#         Returns:
#             bool: 创建是否成功
#         """
#         # 构建匹配属性（使用正确的Cypher语法）
#         from_props_list = [f"{k}: ${f'from_{k}'}" for k in from_node[1].keys()]
#         from_props_str = "{" + ", ".join(from_props_list) + "}"
        
#         to_props_list = [f"{k}: ${f'to_{k}'}" for k in to_node[1].keys()]
#         to_props_str = "{" + ", ".join(to_props_list) + "}"
        
#         # 构建关系属性
#         rel_props_str = ""
#         if properties:
#             rel_props_list = [f"{k}: ${f'rel_{k}'}" for k in properties.keys()]
#             rel_props_str = "{" + ", ".join(rel_props_list) + "}"
        
#         query = f"""
#         MATCH (a:{from_node[0]} {from_props_str})
#         MATCH (b:{to_node[0]} {to_props_str})
#         MERGE (a)-[r:{relationship_type} {rel_props_str}]->(b)
#         RETURN r
#         """
        
#         # 构建参数
#         params = {}
#         for k, v in from_node[1].items():
#             params[f"from_{k}"] = v
#         for k, v in to_node[1].items():
#             params[f"to_{k}"] = v
#         if properties:
#             for k, v in properties.items():
#                 params[f"rel_{k}"] = v
        
#         result = self.execute_query(query, params)
#         return len(result) > 0
    
#     def find_nodes(self, label: str, properties: Dict = None) -> List[Dict]:
#         """
#         查找节点
        
#         Args:
#             label: 节点标签
#             properties: 查找条件
            
#         Returns:
#             List[Dict]: 节点列表
#         """
#         if properties:
#             query = f"MATCH (n:{label}) WHERE n = $props RETURN n"
#             params = {"props": properties}
#         else:
#             query = f"MATCH (n:{label}) RETURN n"
#             params = {}
        
#         return self.execute_query(query, params)
    
#     def find_relationships(self, from_label: str = None, 
#                           to_label: str = None, 
#                           rel_type: str = None) -> List[Dict]:
#         """
#         查找关系
        
#         Args:
#             from_label: 起始节点标签
#             to_label: 目标节点标签
#             rel_type: 关系类型
            
#         Returns:
#             List[Dict]: 关系列表
#         """
#         query_parts = ["MATCH (a)-[r]->(b)"]
#         where_conditions = []
        
#         if from_label:
#             where_conditions.append(f"a:{from_label}")
#         if to_label:
#             where_conditions.append(f"b:{to_label}")
#         if rel_type:
#             where_conditions.append(f"type(r) = '{rel_type}'")
        
#         if where_conditions:
#             query_parts.append("WHERE " + " AND ".join(where_conditions))
        
#         query_parts.append("RETURN a, r, b")
#         query = " ".join(query_parts)
        
#         return self.execute_query(query)
    
#     def get_node_relationships(self, node_id: int, direction: str = "both") -> List[Dict]:
#         """
#         获取节点的所有关系
        
#         Args:
#             node_id: 节点ID
#             direction: 关系方向 (in, out, both)
            
#         Returns:
#             List[Dict]: 关系列表
#         """
#         if direction == "in":
#             query = "MATCH (a)-[r]->(n) WHERE id(n) = $node_id RETURN a, r, n"
#         elif direction == "out":
#             query = "MATCH (n)-[r]->(a) WHERE id(n) = $node_id RETURN n, r, a"
#         else:  # both
#             query = "MATCH (n)-[r]-(a) WHERE id(n) = $node_id RETURN n, r, a"
        
#         return self.execute_query(query, {"node_id": node_id})
    
#     def find_shortest_path(self, from_node: Tuple[str, Dict], 
#                           to_node: Tuple[str, Dict], 
#                           max_depth: int = 10) -> List[Dict]:
#         """
#         查找最短路径
        
#         Args:
#             from_node: 起始节点
#             to_node: 目标节点
#             max_depth: 最大深度
            
#         Returns:
#             List[Dict]: 路径节点列表
#         """
#         query = """
#         MATCH (a:{from_label} {from_props})
#         MATCH (b:{to_label} {to_props})
#         MATCH path = shortestPath((a)-[*1..{max_depth}]-(b))
#         RETURN path
#         """.format(
#             from_label=from_node[0],
#             to_label=to_node[0],
#             max_depth=max_depth
#         )
        
#         params = {
#             "from_props": from_node[1],
#             "to_props": to_node[1]
#         }
        
#         return self.execute_query(query, params)
    
#     def delete_node(self, node_id: int) -> bool:
#         """
#         删除节点及其所有关系
        
#         Args:
#             node_id: 节点ID
            
#         Returns:
#             bool: 删除是否成功
#         """
#         query = "MATCH (n) WHERE id(n) = $node_id DETACH DELETE n RETURN n"
#         result = self.execute_query(query, {"node_id": node_id})
#         return len(result) > 0
    
#     def get_graph_stats(self) -> Dict:
#         """
#         获取图数据库统计信息
        
#         Returns:
#             Dict: 统计信息
#         """
#         node_count_query = "MATCH (n) RETURN count(n) as node_count"
#         rel_count_query = "MATCH ()-[r]->() RETURN count(r) as rel_count"
        
#         node_count = self.execute_query(node_count_query)
#         rel_count = self.execute_query(rel_count_query)
        
#         return {
#             "node_count": node_count[0]["node_count"] if node_count else 0,
#             "relationship_count": rel_count[0]["rel_count"] if rel_count else 0
#         }
    
#     def delete_all_data(self) -> bool:
#         """
#         删除所有节点和关系（清空数据库）
        
#         Returns:
#             bool: 删除是否成功
#         """
#         try:
#             query = "MATCH (n) DETACH DELETE n"
#             self.execute_query(query)
#             logger.info("已清空Neo4j数据库中的所有数据")
#             return True
#         except Exception as e:
#             logger.error(f"清空Neo4j数据库失败: {e}")
#             return False


# # 使用示例
# if __name__ == "__main__":
#     # 初始化图数据库
#     graph_db = Neo4jGraphDB()
    
#     # 创建节点
#     graph_db.create_node("Person", {"name": "张三", "age": 30})
#     graph_db.create_node("Person", {"name": "李四", "age": 25})
#     graph_db.create_node("Company", {"name": "科技公司", "industry": "IT"})
    
#     # 创建关系
#     graph_db.create_relationship(
#         ("Person", {"name": "张三"}),
#         ("Person", {"name": "李四"}),
#         "KNOWS",
#         {"since": "2020"}
#     )
    
#     graph_db.create_relationship(
#         ("Person", {"name": "张三"}),
#         ("Company", {"name": "科技公司"}),
#         "WORKS_FOR",
#         {"position": "工程师"}
#     )
    
#     # 查找节点
#     people = graph_db.find_nodes("Person")
#     print("人员节点:", people)
    
#     # 查找关系
#     relationships = graph_db.find_relationships(rel_type="KNOWS")
#     print("关系:", relationships)
    
#     # 获取统计信息
#     stats = graph_db.get_graph_stats()
#     print("图统计:", stats)
    
#     # 关闭连接
#     graph_db.close()




"""
Neo4j图数据库模块
用于存储和查询知识图谱关系
"""

from neo4j import GraphDatabase
from typing import List, Dict, Any, Optional, Tuple
import logging
import subprocess
import time
import platform
import os
import socket
import urllib.request
import urllib.error
from urllib.parse import urlparse

# 引入Config配置
from app.shared.config import Config

logger = logging.getLogger(__name__)


def check_port_open(host="localhost", port=7687, timeout=2):
    """
    检查端口是否开放
    
    Args:
        host: 主机地址
        port: 端口号
        timeout: 超时时间（秒）
        
    Returns:
        bool: 端口是否开放
    """
    try:
        sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        sock.settimeout(timeout)
        result = sock.connect_ex((host, port))
        sock.close()
        return result == 0
    except:
        return False


def check_neo4j_browser(port=7474, timeout=3):
    """
    检查Neo4j Browser是否可访问
    
    Args:
        port: Neo4j Browser端口（默认7474）
        timeout: 超时时间（秒）
        
    Returns:
        bool: Browser是否可访问
    """
    try:
        # 从Config解析browser端口（如果配置了的话），否则用默认7474
        browser_port = port
        if hasattr(Config, 'NEO4J_BROWSER_PORT') and Config.NEO4J_BROWSER_PORT:
            browser_port = Config.NEO4J_BROWSER_PORT
        
        url = f"http://localhost:{browser_port}"
        response = urllib.request.urlopen(url, timeout=timeout)
        return response.getcode() == 200
    except:
        return False


def check_neo4j_running(
    uri: str = Config.NEO4J_URI,
    username: str = Config.NEO4J_USER,
    password: str = Config.NEO4J_PASSWORD,
):
    """
    检查Neo4j是否正在运行
    
    Args:
        uri: Neo4j URI
        username: 用户名
        password: 密码
        
    Returns:
        bool: 是否正在运行
    """
    # 先检查端口是否开放（更快）
    if "://" in uri:
        host_port = uri.split("://")[1]
        if ":" in host_port:
            host, port = host_port.split(":")
            port = int(port)
        else:
            host = host_port
            port = 7687
    else:
        host = "localhost"
        port = 7687
    
    if not check_port_open(host, port, timeout=2):
        return False
    
    # 端口开放，尝试连接数据库
    try:
        from neo4j import GraphDatabase
        driver = GraphDatabase.driver(uri, auth=(username, password), connection_timeout=3)
        with driver.session() as session:
            session.run("RETURN 1")
        driver.close()
        return True
    except:
        return False


def check_neo4j_installed():
    """
    检查Neo4j是否已安装
    
    Returns:
        tuple: (是否安装, 安装路径, 安装类型)
    """
    system = platform.system()
    installed_paths = []
    install_type = None
    
    if system == "Windows":
        # 检查Neo4j Desktop
        desktop_paths = [
            os.path.expanduser(r'~\AppData\Local\Programs\Neo4j Desktop'),
            r'C:\Program Files\Neo4j Desktop',
            r'C:\Program Files (x86)\Neo4j Desktop',
        ]
        
        # 检查独立安装
        standalone_paths = [
            r'C:\neo4j',
            os.path.expanduser(r'~\neo4j'),
        ]
        
        for path in desktop_paths:
            if os.path.exists(path):
                installed_paths.append(path)
                install_type = "Desktop"
                break
        
        for path in standalone_paths:
            if os.path.exists(path):
                installed_paths.append(path)
                if install_type is None:
                    install_type = "Standalone"
                break
        
        # 检查Windows服务
        try:
            result = subprocess.run(
                ['sc', 'query', 'Neo4j'],
                capture_output=True,
                text=True,
                timeout=5
            )
            if result.returncode == 0 and 'Neo4j' in result.stdout:
                install_type = "Service"
                installed_paths.append("Windows Service")
        except:
            pass
    
    return len(installed_paths) > 0, installed_paths, install_type


def start_neo4j_service(max_wait_time=30):
    """
    尝试启动Neo4j服务并等待其完全启动
    使用 neo4j console 命令在后台启动
    
    Args:
        max_wait_time: 最大等待时间（秒）
        
    Returns:
        bool: 是否成功启动
    """
    logger.info("尝试启动Neo4j服务（使用neo4j console）...")
    
    # 首先检查是否已经在运行（使用Config配置）
    if check_neo4j_running():
        logger.info("Neo4j服务已在运行")
        if check_neo4j_browser():
            # 解析browser端口用于显示
            browser_port = 7474
            if hasattr(Config, 'NEO4J_BROWSER_PORT') and Config.NEO4J_BROWSER_PORT:
                browser_port = Config.NEO4J_BROWSER_PORT
            logger.info(f"Neo4j Browser可访问: http://localhost:{browser_port}")
        return True
    
    # 检查是否已安装
    is_installed, paths, install_type = check_neo4j_installed()
    
    if not is_installed:
        logger.error("未检测到Neo4j安装")
        logger.error("请先安装Neo4j:")
        logger.error("  1. Neo4j Desktop: https://neo4j.com/download/")
        logger.error("  2. 或独立版本: https://neo4j.com/download-center/#community")
        return False
    
    logger.info(f"检测到Neo4j安装类型: {install_type}, 路径: {paths}")
    
    system = platform.system()
    start_success = False
    
    if system == "Windows":
        # Windows系统：尝试多种方式启动
        methods = []
        
        # 方法1: 使用Windows服务（最可靠）
        methods.append({
            'name': 'Windows服务启动',
            'commands': [
                ['net', 'start', 'Neo4j'],
                ['sc', 'start', 'Neo4j'],
            ],
            'check_paths': []
        })
        
        # 方法2: 查找并启动Neo4j Desktop数据库
        if install_type == "Desktop":
            # Neo4j Desktop的数据库路径通常在用户目录下
            desktop_db_paths = [
                os.path.expanduser(r'~\AppData\Roaming\Neo4j Desktop\Application\neo4jDatabases'),
                os.path.expanduser(r'~\AppData\Local\Neo4j Desktop\Application\neo4jDatabases'),
            ]
            
            for db_base in desktop_db_paths:
                if os.path.exists(db_base):
                    # 查找最新的数据库目录
                    for db_dir in os.listdir(db_base):
                        db_path = os.path.join(db_base, db_dir)
                        if os.path.isdir(db_path):
                            # 查找bin目录
                            for root, dirs, files in os.walk(db_path):
                                if 'neo4j.bat' in files or 'neo4j-admin.bat' in files:
                                    bin_dir = root
                                    neo4j_bat = os.path.join(bin_dir, 'neo4j.bat')
                                    if os.path.exists(neo4j_bat):
                                        methods.append({
                                            'name': f'Neo4j Desktop数据库启动 ({db_dir})',
                                            'commands': [],
                                            'check_paths': [neo4j_bat]
                                        })
                                        break
        
        # 方法3: 使用neo4j console启动（后台运行）- 优先使用此方法
        # 查找neo4j.bat或neo4j.cmd
        neo4j_bin_paths = []
        
        # 查找可能的Neo4j安装路径
        search_paths = [
            r'C:\Program Files\Neo4j Desktop',
            r'C:\neo4j',
            os.path.expanduser(r'~\AppData\Local\Programs\Neo4j Desktop'),
            os.path.expanduser(r'~\neo4j'),
            os.path.expanduser(r'~\AppData\Roaming\Neo4j Desktop'),
        ]
        
        for base_path in search_paths:
            if os.path.exists(base_path):
                # 查找bin目录下的neo4j.bat或neo4j.cmd
                for root, dirs, files in os.walk(base_path):
                    if 'bin' in root.lower() or root.endswith('bin'):
                        for file in files:
                            if file.lower() in ['neo4j.bat', 'neo4j.cmd', 'neo4j']:
                                full_path = os.path.join(root, file)
                                if full_path not in neo4j_bin_paths:
                                    neo4j_bin_paths.append(full_path)
        
        # 如果找到neo4j命令，优先使用console模式启动（后台）
        if neo4j_bin_paths:
            # 将console模式添加到方法列表的最前面（优先尝试）
            for neo4j_cmd in neo4j_bin_paths[:3]:  # 只尝试前3个
                methods.insert(0, {  # 插入到最前面，优先尝试
                    'name': f'Neo4j Console启动 ({os.path.basename(neo4j_cmd)})',
                    'commands': [],
                    'check_paths': [neo4j_cmd],
                    'use_console': True  # 标记使用console模式
                })
        
        # 方法4: 使用neo4j start（备用）
        methods.append({
            'name': 'Neo4j Start启动',
            'commands': [
                ['neo4j', 'start'],
                ['neo4j.bat', 'start'],
            ],
            'check_paths': [
                r'C:\Program Files\Neo4j Desktop\neo4j-community\bin\neo4j.bat',
                r'C:\neo4j\bin\neo4j.bat',
                os.path.expanduser(r'~\AppData\Local\Programs\Neo4j Desktop\neo4j-community\bin\neo4j.bat'),
            ],
            'use_console': False
        })
        
        for method in methods:
            logger.info(f"尝试方法: {method['name']}")
            
            # 先尝试直接命令
            for cmd in method['commands']:
                try:
                    result = subprocess.run(
                        cmd,
                        capture_output=True,
                        text=True,
                        timeout=15,
                        shell=True  # Windows需要shell=True
                    )
                    stdout_lower = result.stdout.lower() if result.stdout else ""
                    stderr_lower = result.stderr.lower() if result.stderr else ""
                    output = stdout_lower + stderr_lower
                    
                    if (result.returncode == 0 or 
                        'started' in output or 
                        'running' in output or
                        '已经启动' in output or
                        'already running' in output):
                        logger.info(f"Neo4j启动命令执行成功: {' '.join(cmd)}")
                        start_success = True
                        break
                    else:
                        logger.debug(f"命令输出: {result.stdout} {result.stderr}")
                except (subprocess.TimeoutExpired, FileNotFoundError, Exception) as e:
                    logger.debug(f"命令执行失败: {e}")
                    continue
            if start_success:
                break
            
            # 尝试使用完整路径
            for path in method['check_paths']:
                if os.path.exists(path):
                    try:
                        logger.info(f"尝试启动: {path}")
                        
                        # 检查是否使用console模式
                        use_console = method.get('use_console', False)
                        
                        if use_console:
                            # 使用neo4j console在后台启动
                            # Windows下使用START命令在后台运行
                            if system == "Windows":
                                # 使用START命令在后台运行console
                                # /B 表示不创建新窗口，在后台运行
                                cmd_str = f'START /B "" "{path}" console'
                                try:
                                    # 使用Popen在后台运行，不等待完成
                                    process = subprocess.Popen(
                                        cmd_str,
                                        shell=True,
                                        stdout=subprocess.PIPE,
                                        stderr=subprocess.PIPE,
                                        cwd=os.path.dirname(path),
                                        creationflags=subprocess.CREATE_NO_WINDOW if hasattr(subprocess, 'CREATE_NO_WINDOW') else 0
                                    )
                                    logger.info(f"Neo4j console命令已在后台启动: {path}")
                                    logger.info("等待服务启动（检查端口）...")
                                    start_success = True
                                    break
                                except Exception as e:
                                    logger.debug(f"启动失败: {e}")
                                    continue
                            else:
                                # Linux/Mac使用nohup在后台运行
                                try:
                                    process = subprocess.Popen(
                                        [path, 'console'],
                                        stdout=subprocess.PIPE,
                                        stderr=subprocess.PIPE,
                                        cwd=os.path.dirname(path),
                                        start_new_session=True  # 创建新会话，脱离父进程
                                    )
                                    logger.info(f"Neo4j console命令已在后台启动: {path}")
                                    logger.info("等待服务启动（检查端口）...")
                                    start_success = True
                                    break
                                except Exception as e:
                                    logger.debug(f"启动失败: {e}")
                                    continue
                        else:
                            # 使用start命令
                            result = subprocess.run(
                                [path, 'start'],
                                capture_output=True,
                                text=True,
                                timeout=15,
                                cwd=os.path.dirname(path),
                                shell=True
                            )
                            stdout_lower = result.stdout.lower() if result.stdout else ""
                            stderr_lower = result.stderr.lower() if result.stderr else ""
                            output = stdout_lower + stderr_lower
                            
                            if (result.returncode == 0 or 
                                'started' in output or 
                                'running' in output or
                                '启动' in output):
                                logger.info(f"Neo4j启动命令执行成功: {path}")
                                start_success = True
                                break
                            else:
                                logger.debug(f"启动输出: {result.stdout} {result.stderr}")
                    except Exception as e:
                        logger.debug(f"启动失败: {e}")
                        continue
                if start_success:
                    break
        
        # 如果还没成功，尝试直接使用neo4j console命令（从PATH中查找）
        if not start_success:
            logger.info("尝试使用neo4j console命令启动（从系统PATH）...")
            try:
                # Windows下尝试直接运行neo4j console
                if system == "Windows":
                    # 使用START命令在后台运行
                    cmd_str = 'START /B "" neo4j console'
                    process = subprocess.Popen(
                        cmd_str,
                        shell=True,
                        stdout=subprocess.PIPE,
                        stderr=subprocess.PIPE,
                        creationflags=subprocess.CREATE_NO_WINDOW if hasattr(subprocess, 'CREATE_NO_WINDOW') else 0
                    )
                    logger.info("neo4j console命令已执行（后台运行）")
                    logger.info("等待服务启动（检查端口）...")
                    start_success = True
                else:
                    # Linux/Mac
                    process = subprocess.Popen(
                        ['neo4j', 'console'],
                        stdout=subprocess.PIPE,
                        stderr=subprocess.PIPE,
                        start_new_session=True
                    )
                    logger.info("neo4j console命令已执行（后台运行）")
                    logger.info("等待服务启动（检查端口）...")
                    start_success = True
            except FileNotFoundError:
                logger.debug("neo4j命令未在PATH中找到")
            except Exception as e:
                logger.debug(f"neo4j console命令执行失败: {e}")
    
    elif system == "Linux" or system == "Darwin":  # Linux or Mac
        # Linux/Mac: 使用neo4j命令
        try:
            result = subprocess.run(
                ['neo4j', 'start'],
                capture_output=True,
                text=True,
                timeout=10
            )
            if result.returncode == 0:
                logger.info("Neo4j启动成功")
                return True
        except FileNotFoundError:
            # 尝试使用完整路径
            common_paths = [
                '/usr/local/neo4j/bin/neo4j',
                '/opt/neo4j/bin/neo4j',
                os.path.expanduser('~/neo4j/bin/neo4j'),
            ]
            for path in common_paths:
                if os.path.exists(path):
                    try:
                        result = subprocess.run(
                            [path, 'start'],
                            capture_output=True,
                            text=True,
                            timeout=10
                        )
                        if result.returncode == 0:
                            logger.info(f"Neo4j启动成功: {path}")
                            return True
                    except Exception:
                        continue
    
    # 如果启动命令执行成功，等待服务完全启动
    if start_success:
        # 从Config解析主机和端口
        parsed = urlparse(Config.NEO4J_URI)
        host = parsed.hostname or "localhost"
        port = parsed.port or 7687
        
        logger.info(f"等待Neo4j服务完全启动（最多{max_wait_time}秒）...")
        start_time = time.time()
        
        while time.time() - start_time < max_wait_time:
            # 检查Bolt端口
            if check_port_open(host, port, timeout=2):
                logger.info(f"Neo4j Bolt端口({port})已开放")
                # 再等待一下让服务完全就绪
                time.sleep(3)
                
                # 检查数据库连接（使用Config配置）
                if check_neo4j_running():
                    logger.info("✓ Neo4j数据库连接成功")
                    logger.info(f"  用户名: {Config.NEO4J_USER}")
                    logger.info(f"  密码: {'*' * len(Config.NEO4J_PASSWORD)}")
                    
                    # 检查Browser
                    browser_port = 7474
                    if hasattr(Config, 'NEO4J_BROWSER_PORT') and Config.NEO4J_BROWSER_PORT:
                        browser_port = Config.NEO4J_BROWSER_PORT
                        
                    if check_neo4j_browser(port=browser_port):
                        logger.info(f"✓ Neo4j Browser可访问: http://localhost:{browser_port}")
                        logger.info("  可以在浏览器中打开Neo4j Browser进行可视化操作")
                        logger.info(f"  登录信息: 用户名={Config.NEO4J_USER}, 密码={Config.NEO4J_PASSWORD}")
                    else:
                        logger.warning(f"Neo4j Browser可能还未完全启动，请稍后访问: http://localhost:{browser_port}")
                        logger.info(f"  登录信息: 用户名={Config.NEO4J_USER}, 密码={Config.NEO4J_PASSWORD}")
                    
                    return True
                else:
                    logger.debug("端口已开放但数据库连接失败，继续等待...")
            
            time.sleep(2)
            elapsed = int(time.time() - start_time)
            if elapsed % 5 == 0:
                logger.info(f"  等待中... ({elapsed}/{max_wait_time}秒)")
        
        logger.warning("Neo4j启动超时，但可能仍在启动中")
        logger.warning(f"请手动检查: http://localhost:{browser_port if 'browser_port' in locals() else 7474}")
        return False
    
    logger.warning("无法自动启动Neo4j")
    logger.warning("=" * 60)
    logger.warning("Neo4j启动指南:")
    logger.warning("=" * 60)
    logger.warning("如果使用Neo4j Desktop:")
    logger.warning("  1. 打开Neo4j Desktop应用程序")
    logger.warning("  2. 创建或选择一个数据库项目")
    logger.warning("  3. 点击'Start'按钮启动数据库")
    logger.warning("  4. 确保数据库状态显示为'Running'")
    logger.warning("")
    logger.warning("如果使用独立安装:")
    logger.warning("  1. 打开命令行（以管理员身份）")
    logger.warning("  2. 进入Neo4j安装目录的bin文件夹")
    logger.warning("  3. 运行: neo4j.bat start")
    logger.warning("")
    logger.warning("如果使用Windows服务:")
    logger.warning("  1. 打开服务管理器 (services.msc)")
    logger.warning("  2. 找到Neo4j服务")
    logger.warning("  3. 右键点击并选择'启动'")
    logger.warning("")
    logger.warning("检查Neo4j是否运行:")
    browser_port = 7474
    if hasattr(Config, 'NEO4J_BROWSER_PORT') and Config.NEO4J_BROWSER_PORT:
        browser_port = Config.NEO4J_BROWSER_PORT
    logger.warning(f"  - 打开浏览器访问: http://localhost:{browser_port}")
    logger.warning("  - 如果能打开Neo4j Browser，说明服务已启动")
    logger.warning("=" * 60)
    return False


class Neo4jGraphDB:
    """Neo4j图数据库管理器"""
    
    def __init__(self, uri: str = Config.NEO4J_URI, 
                 username: str = Config.NEO4J_USER, 
                 password: str = Config.NEO4J_PASSWORD,
                 auto_start: bool = True, max_retries: int = 3):
        """
        初始化Neo4j连接
        
        Args:
            uri: Neo4j数据库URI
            username: 用户名
            password: 密码
            auto_start: 是否在连接失败时自动尝试启动Neo4j服务
            max_retries: 最大重试次数
        """
        self.uri = uri
        self.username = username
        self.password = password
        self.auto_start = auto_start
        self.max_retries = max_retries
        self.driver = None
        self._connect_with_retry()
    
    def _connect_with_retry(self):
        """建立数据库连接，失败时自动重试"""
        self.driver = None
        
        # 首先检查Neo4j是否已经在运行（使用配置的用户名密码）
        if check_neo4j_running(self.uri, self.username, self.password):
            logger.info("检测到Neo4j服务已在运行")
            try:
                self.driver = GraphDatabase.driver(
                    self.uri, 
                    auth=(self.username, self.password),
                    connection_timeout=5
                )
                with self.driver.session() as session:
                    session.run("RETURN 1")
                logger.info("Neo4j连接建立成功")
                return
            except Exception as e:
                logger.warning(f"虽然检测到服务运行，但连接失败: {e}")
                logger.warning("可能是用户名或密码错误")
                logger.warning(f"当前配置: 用户名={self.username}, 密码={'*' * len(self.password)}")
        
        # 检查Neo4j是否已安装
        is_installed, paths, install_type = check_neo4j_installed()
        if not is_installed and self.auto_start:
            logger.error("=" * 60)
            logger.error("Neo4j未安装，无法自动启动")
            logger.error("=" * 60)
            logger.error("请先安装Neo4j:")
            logger.error("  1. Neo4j Desktop (推荐): https://neo4j.com/download/")
            logger.error("  2. 或独立版本: https://neo4j.com/download-center/#community")
            logger.error("=" * 60)
            return
        
        for attempt in range(self.max_retries):
            try:
                self.driver = GraphDatabase.driver(
                    self.uri, 
                    auth=(self.username, self.password),
                    connection_timeout=5  # 5秒连接超时
                )
                # 测试连接
                with self.driver.session() as session:
                    session.run("RETURN 1")
                logger.info("Neo4j连接建立成功")
                return
            except Exception as e:
                error_msg = str(e)
                if attempt < self.max_retries - 1:
                    logger.warning(f"Neo4j连接失败 (尝试 {attempt + 1}/{self.max_retries})")
                    logger.debug(f"错误详情: {error_msg}")
                    
                    if self.auto_start:
                        logger.info("尝试启动Neo4j服务...")
                        if start_neo4j_service():
                            logger.info("等待Neo4j服务启动（10秒）...")
                            time.sleep(10)  # 增加等待时间
                        else:
                            logger.warning("无法自动启动Neo4j服务，请手动启动")
                            # 只尝试启动一次，避免重复尝试
                            self.auto_start = False
                            time.sleep(2)
                    else:
                        time.sleep(2)
                else:
                    logger.error(f"Neo4j连接最终失败")
                    logger.error(f"错误: {error_msg}")
                    logger.error("")
                    logger.error("请检查:")
                    logger.error("  1. Neo4j服务是否已启动")
                    logger.error(f"  2. 连接URI是否正确: {self.uri}")
                    logger.error("  3. 用户名和密码是否正确")
                    logger.error("  4. 防火墙是否阻止了连接")
                    
                    # 解析browser端口用于提示
                    browser_port = 7474
                    if hasattr(Config, 'NEO4J_BROWSER_PORT') and Config.NEO4J_BROWSER_PORT:
                        browser_port = Config.NEO4J_BROWSER_PORT
                    logger.error(f"  5. 尝试访问: http://localhost:{browser_port} 查看Neo4j Browser")
                    self.driver = None
    
    def _connect(self):
        """建立数据库连接（已弃用，使用_connect_with_retry）"""
        self._connect_with_retry()
    
    def close(self):
        """关闭数据库连接"""
        if self.driver:
            self.driver.close()
            logger.info("Neo4j连接已关闭")
    
    def execute_query(self, query: str, parameters: Dict = None) -> List[Dict]:
        """
        执行Cypher查询
        
        Args:
            query: Cypher查询语句
            parameters: 查询参数
            
        Returns:
            List[Dict]: 查询结果
        """
        if not self.driver:
            logger.error("数据库未连接")
            return []
        
        try:
            with self.driver.session() as session:
                result = session.run(query, parameters or {})
                return [record.data() for record in result]
        except Exception as e:
            logger.error(f"查询执行失败: {e}")
            return []
    
    def create_node(self, label: str, properties: Dict) -> bool:
        """
        创建节点
        
        Args:
            label: 节点标签
            properties: 节点属性
            
        Returns:
            bool: 创建是否成功
        """
        query = f"CREATE (n:{label} $properties) RETURN n"
        result = self.execute_query(query, {"properties": properties})
        return len(result) > 0
    
    def merge_node(self, label: str, match_properties: Dict, set_properties: Dict = None) -> bool:
        """
        合并节点（如果不存在则创建，存在则更新）
        
        Args:
            label: 节点标签
            match_properties: 用于匹配的属性（唯一标识）
            set_properties: 要设置的属性
            
        Returns:
            bool: 操作是否成功
        """
        # 构建MERGE的匹配属性
        match_props_list = [f"{k}: ${f'match_{k}'}" for k in match_properties.keys()]
        match_props_str = "{" + ", ".join(match_props_list) + "}"
        
        # 构建参数
        params = {}
        for k, v in match_properties.items():
            params[f"match_{k}"] = v
        
        # 构建SET语句
        if set_properties:
            set_statements = ", ".join([f"n.{k} = ${f'set_{k}'}" for k in set_properties.keys()])
            for k, v in set_properties.items():
                params[f"set_{k}"] = v
            
            query = f"""
            MERGE (n:{label} {match_props_str})
            ON CREATE SET {set_statements}
            ON MATCH SET {set_statements}
            RETURN n
            """
        else:
            query = f"""
            MERGE (n:{label} {match_props_str})
            RETURN n
            """
        
        result = self.execute_query(query, params)
        return len(result) > 0
    
    def create_relationship(self, from_node: Tuple[str, Dict], 
                          to_node: Tuple[str, Dict], 
                          relationship_type: str, 
                          properties: Dict = None) -> bool:
        """
        创建关系
        
        Args:
            from_node: 起始节点 (label, properties)
            to_node: 目标节点 (label, properties)
            relationship_type: 关系类型
            properties: 关系属性
            
        Returns:
            bool: 创建是否成功
        """
        # 构建匹配属性（使用正确的Cypher语法）
        from_props_list = [f"{k}: ${f'from_{k}'}" for k in from_node[1].keys()]
        from_props_str = "{" + ", ".join(from_props_list) + "}"
        
        to_props_list = [f"{k}: ${f'to_{k}'}" for k in to_node[1].keys()]
        to_props_str = "{" + ", ".join(to_props_list) + "}"
        
        # 构建关系属性
        rel_props_str = ""
        if properties:
            rel_props_list = [f"{k}: ${f'rel_{k}'}" for k in properties.keys()]
            rel_props_str = "{" + ", ".join(rel_props_list) + "}"
        
        query = f"""
        MATCH (a:{from_node[0]} {from_props_str})
        MATCH (b:{to_node[0]} {to_props_str})
        MERGE (a)-[r:{relationship_type} {rel_props_str}]->(b)
        RETURN r
        """
        
        # 构建参数
        params = {}
        for k, v in from_node[1].items():
            params[f"from_{k}"] = v
        for k, v in to_node[1].items():
            params[f"to_{k}"] = v
        if properties:
            for k, v in properties.items():
                params[f"rel_{k}"] = v
        
        result = self.execute_query(query, params)
        return len(result) > 0
    
    def find_nodes(self, label: str, properties: Dict = None) -> List[Dict]:
        """
        查找节点
        
        Args:
            label: 节点标签
            properties: 查找条件
            
        Returns:
            List[Dict]: 节点列表
        """
        if properties:
            query = f"MATCH (n:{label}) WHERE n = $props RETURN n"
            params = {"props": properties}
        else:
            query = f"MATCH (n:{label}) RETURN n"
            params = {}
        
        return self.execute_query(query, params)
    
    def find_relationships(self, from_label: str = None, 
                          to_label: str = None, 
                          rel_type: str = None) -> List[Dict]:
        """
        查找关系
        
        Args:
            from_label: 起始节点标签
            to_label: 目标节点标签
            rel_type: 关系类型
            
        Returns:
            List[Dict]: 关系列表
        """
        query_parts = ["MATCH (a)-[r]->(b)"]
        where_conditions = []
        
        if from_label:
            where_conditions.append(f"a:{from_label}")
        if to_label:
            where_conditions.append(f"b:{to_label}")
        if rel_type:
            where_conditions.append(f"type(r) = '{rel_type}'")
        
        if where_conditions:
            query_parts.append("WHERE " + " AND ".join(where_conditions))
        
        query_parts.append("RETURN a, r, b")
        query = " ".join(query_parts)
        
        return self.execute_query(query)
    
    def get_node_relationships(self, node_id: int, direction: str = "both") -> List[Dict]:
        """
        获取节点的所有关系
        
        Args:
            node_id: 节点ID
            direction: 关系方向 (in, out, both)
            
        Returns:
            List[Dict]: 关系列表
        """
        if direction == "in":
            query = "MATCH (a)-[r]->(n) WHERE id(n) = $node_id RETURN a, r, n"
        elif direction == "out":
            query = "MATCH (n)-[r]->(a) WHERE id(n) = $node_id RETURN n, r, a"
        else:  # both
            query = "MATCH (n)-[r]-(a) WHERE id(n) = $node_id RETURN n, r, a"
        
        return self.execute_query(query, {"node_id": node_id})
    
    def find_shortest_path(self, from_node: Tuple[str, Dict], 
                          to_node: Tuple[str, Dict], 
                          max_depth: int = 10) -> List[Dict]:
        """
        查找最短路径
        
        Args:
            from_node: 起始节点
            to_node: 目标节点
            max_depth: 最大深度
            
        Returns:
            List[Dict]: 路径节点列表
        """
        query = """
        MATCH (a:{from_label} {from_props})
        MATCH (b:{to_label} {to_props})
        MATCH path = shortestPath((a)-[*1..{max_depth}]-(b))
        RETURN path
        """.format(
            from_label=from_node[0],
            to_label=to_node[0],
            max_depth=max_depth
        )
        
        params = {
            "from_props": from_node[1],
            "to_props": to_node[1]
        }
        
        return self.execute_query(query, params)
    
    def delete_node(self, node_id: int) -> bool:
        """
        删除节点及其所有关系
        
        Args:
            node_id: 节点ID
            
        Returns:
            bool: 删除是否成功
        """
        query = "MATCH (n) WHERE id(n) = $node_id DETACH DELETE n RETURN n"
        result = self.execute_query(query, {"node_id": node_id})
        return len(result) > 0
    
    def get_graph_stats(self) -> Dict:
        """
        获取图数据库统计信息
        
        Returns:
            Dict: 统计信息
        """
        node_count_query = "MATCH (n) RETURN count(n) as node_count"
        rel_count_query = "MATCH ()-[r]->() RETURN count(r) as rel_count"
        
        node_count = self.execute_query(node_count_query)
        rel_count = self.execute_query(rel_count_query)
        
        return {
            "node_count": node_count[0]["node_count"] if node_count else 0,
            "relationship_count": rel_count[0]["rel_count"] if rel_count else 0
        }
    
    def delete_all_data(self) -> bool:
        """
        删除所有节点和关系（清空数据库）
        
        Returns:
            bool: 删除是否成功
        """
        try:
            query = "MATCH (n) DETACH DELETE n"
            self.execute_query(query)
            logger.info("已清空Neo4j数据库中的所有数据")
            return True
        except Exception as e:
            logger.error(f"清空Neo4j数据库失败: {e}")
            return False


# 使用示例
if __name__ == "__main__":
    # 初始化图数据库（使用Config配置）
    graph_db = Neo4jGraphDB()
    
    # 创建节点
    graph_db.create_node("Person", {"name": "张三", "age": 30})
    graph_db.create_node("Person", {"name": "李四", "age": 25})
    graph_db.create_node("Company", {"name": "科技公司", "industry": "IT"})
    
    # 创建关系
    graph_db.create_relationship(
        ("Person", {"name": "张三"}),
        ("Person", {"name": "李四"}),
        "KNOWS",
        {"since": "2020"}
    )
    
    graph_db.create_relationship(
        ("Person", {"name": "张三"}),
        ("Company", {"name": "科技公司"}),
        "WORKS_FOR",
        {"position": "工程师"}
    )
    
    # 查找节点
    people = graph_db.find_nodes("Person")
    print("人员节点:", people)
    
    # 查找关系
    relationships = graph_db.find_relationships(rel_type="KNOWS")
    print("关系:", relationships)
    
    # 获取统计信息
    stats = graph_db.get_graph_stats()
    print("图统计:", stats)
    
    # 关闭连接
    graph_db.close()