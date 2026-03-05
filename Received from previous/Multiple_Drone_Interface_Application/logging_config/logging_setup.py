"""
Central logging configuration for the application.

Best practices:
- Library modules should not configure logging; they should use a module-level
  logger via logging.getLogger(__name__) and leave configuration to the app.
- The application configures root handlers/formatters once at startup.
"""

from __future__ import annotations

import logging
from typing import Optional
from logging.handlers import RotatingFileHandler
import os


def init_logging(level: int = logging.INFO, enable_console: bool = True) -> None:
    """Initialize root logging if not already configured.

    - Sets the root level.
    - Adds a console StreamHandler with a concise format if requested and if
      no handlers are present yet.
    """
    root = logging.getLogger()
    root.setLevel(level)

    if enable_console:
        # Avoid adding duplicate handlers
        has_stream = any(isinstance(h, logging.StreamHandler) for h in root.handlers)
        if not has_stream:
            handler = logging.StreamHandler()
            fmt = logging.Formatter(
                "%(asctime)s %(levelname)s: %(message)s", datefmt="%H:%M:%S"
            )
            handler.setFormatter(fmt)
            root.addHandler(handler)


def init_file_logging(
    base_dir: Optional[str] = None,
    filename: str = "drone_app.log",
    level: int = logging.INFO,
    max_bytes: int = 5 * 1024 * 1024,
    backup_count: int = 5,
) -> None:
    """Attach a rotating file handler and quiet noisy third-party loggers.

    - Creates `logs/filename` under `base_dir` (or this file's folder) if needed.
    - Adds a RotatingFileHandler to the root logger if not already present.
    - Sets cflib loggers to WARNING to reduce noise.
    """
    root = logging.getLogger()
    root.setLevel(level)

    if base_dir is None:
        base_dir = os.path.dirname(os.path.dirname(__file__))
    log_dir = os.path.join(base_dir, "logs")
    try:
        os.makedirs(log_dir, exist_ok=True)
    except Exception:
        # If directory creation fails, silently skip file logging
        return

    log_path = os.path.join(log_dir, filename)
    # Avoid duplicating handlers
    for h in root.handlers:
        if isinstance(h, RotatingFileHandler) and getattr(h, "baseFilename", None) == os.path.abspath(log_path):
            break
    else:
        try:
            fh = RotatingFileHandler(
                log_path, maxBytes=max_bytes, backupCount=backup_count, encoding="utf-8"
            )
            fmt = logging.Formatter(
                "%(asctime)s %(levelname)s: %(message)s", datefmt="%H:%M:%S"
            )
            fh.setFormatter(fmt)
            root.addHandler(fh)
        except Exception:
            pass

    # Quiet cflib if available
    try:
        logging.getLogger("cflib.crtp").setLevel(logging.WARNING)
        logging.getLogger("cflib").setLevel(logging.WARNING)
    except Exception:
        pass


def get_logger(name: Optional[str] = None) -> logging.Logger:
    """Return a named logger with a NullHandler attached by default.

    This prevents "No handler found" warnings if the application did not
    call init_logging yet.
    """
    logger = logging.getLogger(name)
    if not logger.handlers:
        logger.addHandler(logging.NullHandler())
    return logger
