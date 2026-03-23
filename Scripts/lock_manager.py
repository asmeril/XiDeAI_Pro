import os
import sys
import time
import json
import random
from datetime import datetime, timezone

# Platform specific locking constants
if os.name == 'nt':
    import msvcrt
else:
    import fcntl

# Configuration
LOCK_DIR = r"C:\XHive\locks"
LOCK_FILE = os.path.join(LOCK_DIR, "x_session.lock")

# Custom Exceptions
class LockTimeoutError(Exception):
    pass

class LockStaleButBusyError(Exception):
    pass

class LockOwnedByAnotherProcessError(Exception):
    pass

def ensure_lock_dir():
    """Ensure the lock directory exists"""
    if not os.path.exists(LOCK_DIR):
        try:
            os.makedirs(LOCK_DIR, exist_ok=True)
        except OSError as e:
            print(f"Error creating lock workspace: {e}", file=sys.stderr)

def get_process_info():
    """Get current process info safely"""
    pid = os.getpid()
    try:
        # Try to get meaningful name if psutil exists, else generic
        # (Assuming psutil might not be available, keeping it dependency-free for robustness)
        import psutil
        name = psutil.Process(pid).name()
    except ImportError:
        name = f"python_proc_{pid}"
    except Exception:
        name = "unknown"
    return pid, name

def _remove_lock_file_with_retry(lock_path: str, attempts: int = 10, min_sleep: float = 0.2, max_sleep: float = 0.6) -> None:
    """
    Attempt to remove lock file with exponential backoff/retry.
    Resilient against Windows 'file used by another process' errors.
    """
    last_error = None
    
    for i in range(attempts):
        try:
            if not os.path.exists(lock_path):
                return # Already gone
                
            os.remove(lock_path)
            return # Success
            
        except (PermissionError, OSError) as e:
            last_error = e
            # On Windows, error 32 is "The process cannot access the file because it is being used by another process"
            
            wait_time = random.uniform(min_sleep, max_sleep)
            print(f"Lock file busy during removal, retry {i+1}/{attempts} (Wait: {wait_time:.2f}s)", file=sys.stderr)
            time.sleep(wait_time)
            
    # Final check after retries
    if os.path.exists(lock_path):
        print(f"STALE lock could not be removed after {attempts} attempts, failing closed.", file=sys.stderr)
        raise last_error or OSError(f"Could not remove {lock_path}")

def acquire_lock(timeout_seconds=360, stale_seconds=100):  # Reduced from 600s to slightly above C# 90s timeout
    """
    Acquire file-based lock for X session.
    
    Args:
        timeout_seconds: Max time to wait for lock to become free
        stale_seconds: Max age of lock file before considering it dead
        
    Returns:
        True if acquired
        
    Raises:
        LockTimeoutError: If lock cannot be acquired within timeout
        LockStaleButBusyError: If stale lock exists but cannot be removed
    """
    ensure_lock_dir()
    start_time = time.time()
    
    print(f"Attempting to acquire lock: {LOCK_FILE} (Timeout: {timeout_seconds}s)", file=sys.stderr)
    
    while True:
        # 1. Check Timeout
        if (time.time() - start_time) > timeout_seconds:
            raise LockTimeoutError(f"Could not acquire lock within {timeout_seconds} seconds.")

        # 2. Atomic Create Attempt
        fd = None
        try:
            # O_CREAT | O_EXCL ensures this operation fails if file exists
            fd = os.open(LOCK_FILE, os.O_CREAT | os.O_EXCL | os.O_RDWR)
            
            # If we got here, we own the file. Write info.
            pid, pname = get_process_info()
            info = {
                "pid": pid,
                "process_name": pname,
                "created_at_utc": datetime.now(timezone.utc).isoformat()
            }
            
            with os.fdopen(fd, 'w') as f:
                json.dump(info, f)
                
            print(f"Lock acquired by PID {pid} ({pname})", file=sys.stderr)
            return True

        except FileExistsError:
            # Lock exists. Check if stale.
            try:
                if os.path.exists(LOCK_FILE):
                    try:
                        with open(LOCK_FILE, 'r') as f:
                            data = json.load(f)
                            created_str = data.get("created_at_utc")
                            
                            if created_str:
                                created_at = datetime.fromisoformat(created_str)
                                # Fix: Ensure timezone awareness for comparison
                                if created_at.tzinfo is None:
                                    created_at = created_at.replace(tzinfo=timezone.utc)
                                    
                                age = (datetime.now(timezone.utc) - created_at).total_seconds()
                                
                                # v4.3.5: Also check if owner PID is actually running
                                owner_pid = data.get("pid")
                                pid_alive = False
                                if owner_pid:
                                    try:
                                        import psutil
                                        pid_alive = psutil.pid_exists(owner_pid) and psutil.Process(owner_pid).is_running()
                                    except: pid_alive = False
                                
                                if age > stale_seconds or (age > 30 and not pid_alive):
                                    reason = f"Age: {age:.0f}s > {stale_seconds}s" if age > stale_seconds else f"Owner PID {owner_pid} is dead"
                                    print(f"Found STALE lock ({reason}). Forcefully clearing...", file=sys.stderr)
                                    
                                    # v3.9.3: If lock is stale, try to kill the owner process if it's still running
                                    try:
                                        target_pid = data.get("pid")
                                        if target_pid and target_pid != os.getpid():
                                            import psutil
                                            if psutil.pid_exists(target_pid):
                                                proc = psutil.Process(target_pid)
                                                print(f"Force-killing owner of stale lock: PID {target_pid} ({proc.name()})", file=sys.stderr)
                                                proc.kill()
                                                time.sleep(1) # Wait for OS release
                                    except: pass # Ignore if cannot kill or psutil missing
                                    
                                    try:
                                        _remove_lock_file_with_retry(LOCK_FILE)
                                        continue # Retry immediately
                                    except Exception as rem_err:
                                        # If still cannot remove, try one last time by over-writing (truncating)
                                        try:
                                            with open(LOCK_FILE, 'w') as f: f.write("")
                                            os.remove(LOCK_FILE)
                                            continue
                                        except:
                                            raise LockStaleButBusyError(f"Stale lock at {LOCK_FILE} is busy and could not be cleared: {str(rem_err)}")
                                        
                    except (json.JSONDecodeError, ValueError):
                         # Corrupted file? Try to remove it carefully
                         try:
                             # Check mtime just in case it's actually fresh but empty
                             mtime = os.path.getmtime(LOCK_FILE)
                             if (time.time() - mtime) > 10: # If >10s old and corrupted, kill it
                                 _remove_lock_file_with_retry(LOCK_FILE)
                                 continue
                         except: pass
                    except OSError:
                        pass # File read error
                        
            except LockStaleButBusyError as e:
                print(f"Warning: {e} - Will retry...", file=sys.stderr)
                time.sleep(1)
                continue # Retry outer loop
            except Exception as e:
                print(f"Error checking existing lock: {e}", file=sys.stderr)

            # 3. Wait and Retry
            wait_time = random.uniform(2, 5) # Faster retry for smoother experience
            time.sleep(wait_time)
            
        except OSError as e:
            # Unexpected OS error
            print(f"OS Error acquiring lock: {e}", file=sys.stderr)
            raise e

def release_lock():
    """Release the lock if owned by this process"""
    if not os.path.exists(LOCK_FILE):
        print("No lock to release.", file=sys.stderr)
        return

    try:
        current_pid = os.getpid()
        
        # Read lock to verify ownership
        try:
            with open(LOCK_FILE, 'r') as f:
                data = json.load(f)
                lock_pid = data.get("pid")
                
                if lock_pid != current_pid:
                    print(f"Cannot release lock: Owned by PID {lock_pid} (Current: {current_pid})", file=sys.stderr)
                    return
        except FileNotFoundError:
            return # Already gone
        except json.JSONDecodeError:
            print("Corrupted lock file found during release check.", file=sys.stderr)
            return

        # Delete it with light retry logic
        for i in range(3):
            try:
                os.remove(LOCK_FILE)
                print(f"Lock released by PID {current_pid}", file=sys.stderr)
                return
            except (PermissionError, OSError) as e:
                time.sleep(0.2)
                if i == 2:
                     print(f"Warning: Could not remove lock file during release: {e}", file=sys.stderr)

    except Exception as e:
        print(f"Error releasing lock: {e}", file=sys.stderr)

def _test_stale_removal_simulation():
    """
    MANUAL TEST SNIPPET: Simulates a permission error during removal.
    Run this only for debugging logic.
    """
    print("--- MANUAL TEST: Stale Removal Simulation ---", file=sys.stderr)
    
    # Create a dummy stale lock
    ensure_lock_dir()
    with open(LOCK_FILE, 'w') as f:
        json.dump({"pid": 9999, "created_at_utc": "2020-01-01T00:00:00+00:00"}, f)
        
    print(f"Created dummy lock at {LOCK_FILE}", file=sys.stderr)
    
    # Mock os.remove to fail a few times then succeed
    original_remove = os.remove
    fail_count = 0
    
    def mocked_remove(path):
        nonlocal fail_count
        if fail_count < 3:
            fail_count += 1
            print(f"[SIMULATION] Simulating WinError 32 (Attempt {fail_count})", file=sys.stderr)
            raise PermissionError("[WinError 32] The process cannot access the file...")
        else:
            print("[SIMULATION] Success removal", file=sys.stderr)
            original_remove(path)
            
    # Apply monkeypatch
    os.remove = mocked_remove
    
    try:
        print("Calling _remove_lock_file_with_retry...", file=sys.stderr)
        _remove_lock_file_with_retry(LOCK_FILE, attempts=5, min_sleep=0.1, max_sleep=0.2)
        print("TEST PASSED: Removal succeeded after retries.", file=sys.stderr)
    except Exception as e:
        print(f"TEST FAILED: {e}", file=sys.stderr)
    finally:
        os.remove = original_remove # Restore
        if os.path.exists(LOCK_FILE):
             try: original_remove(LOCK_FILE) 
             except: pass

if __name__ == "__main__":
    # CLI Interface for testing or external invocation
    # Usage: python lock_manager.py acquire
    # Usage: python lock_manager.py release
    # Usage: python lock_manager.py test_sim
    
    if len(sys.argv) < 2:
        print("Usage: lock_manager.py [acquire|release|test_sim]", file=sys.stderr)
        sys.exit(1)
        
    action = sys.argv[1].lower()
    
    try:
        if action == "acquire":
            acquire_lock()
            print("SUCCESS") 
        elif action == "release":
            release_lock()
            print("SUCCESS")
        elif action == "test_sim":
            _test_stale_removal_simulation()
        else:
            print(f"Unknown action: {action}", file=sys.stderr)
            sys.exit(1)
            
    except LockTimeoutError:
        print("TIMEOUT", file=sys.stderr)
        sys.exit(1)
    except LockStaleButBusyError:
        print("STALE_BUSY", file=sys.stderr)
        sys.exit(1)
    except Exception as e:
        print(f"ERROR: {e}", file=sys.stderr)
        sys.exit(1)
