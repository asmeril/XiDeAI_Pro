# -*- coding: utf-8 -*-
import time

with open('Scripts/x_daemon.py', 'r', encoding='utf-8') as f:
    content = f.read()

start_marker = '                  # 2. Find "Add another Tweet" button [Robust Selectors]'
end_marker = '                  else:\n                      log(f"Warning: \'Add\' button not found for part {i+1}. Attempting direct fallback...")'

if start_marker in content and end_marker in content:
    part1_split = content.split(start_marker, 1)
    before = part1_split[0]
    part2_split = part1_split[1].split(end_marker, 1)
    after = part2_split[1]

    replacement = '''                  # 2. Find "Add another Tweet" button [Robust Selectors - Ported from IdealSmartNotifier]
                  add_btn = None
                  
                  # Try data-testid first
                  try:
                      btns = driver.find_elements(By.CSS_SELECTOR, "[data-testid='addButton']")
                      for b in btns:
                          if b.is_displayed():
                              add_btn = b
                              log("Found Add Button via data-testid='addButton'")
                              break
                  except: pass
                  
                  # Try labels (localized and expanded)
                  if not add_btn:
                      valid_labels = [
                          "Tweet ekle", "Add Tweet", "Add another Tweet", 
                          "Başka bir gönderi ekle", "Gönderi ekle", "Zincir ekle", 
                          "Add", "Ekle", "Gönderi Ekle", "Post ekle", "Add post",
                          "Yeni gönderi ekle", "New post"
                      ]
                      
                      xpath_parts = [f"@aria-label='{label}'" for label in valid_labels]
                      xpath_join = " or ".join(xpath_parts)
                      xpath = f"//div[@role='button'][{xpath_join}] | //button[{xpath_join}]"
                      
                      try:
                          btns = driver.find_elements(By.XPATH, xpath)
                          for b in btns:
                              if b.is_displayed():
                                  label = (b.get_attribute("aria-label") or "").lower()
                                  
                                  # NUCLEAR EXCLUSION LIST: Ban all other toolbar icons
                                  forbidden_terms = [
                                      "medya", "media", "fotoğraf", "photo", "video", "gif", 
                                      "anket", "poll", "emoji", "planla", "schedule", 
                                      "konum", "location", "kalın", "bold", "italik", "italic", "liste", "list"
                                  ]
                                  
                                  if any(term in label for term in forbidden_terms):
                                      continue
                                      
                                  add_btn = b
                                  log(f"Found Add Button via label: {label}")
                                  break
                      except: pass
                  
                  # Fallback to SVG plus icon detection
                  if not add_btn:
                      try:
                          plus_candidates = driver.find_elements(By.CSS_SELECTOR, 
                              "div[role='button']:has(svg path[d*='M11 11V4h2v7h7v2h-7v7h-2v-7H4v-2h7z']), " +
                              "button:has(svg path[d*='M11 11V4h2v7h7v2h-7v7h-2v-7H4v-2h7z'])"
                          )
                          for b in plus_candidates:
                              if b.is_displayed():
                                  add_btn = b
                                  log("Found Add Button via SVG path")
                                  break
                      except: pass

                  if add_btn:
                      # AGGRESSIVE SCROLL & CLICK LOGIC
                      try:
                          driver.execute_script("arguments[0].scrollIntoView({behavior: 'instant', block: 'center'});", add_btn)
                          time.sleep(0.5)
                          # Click via JS
                          driver.execute_script("arguments[0].click();", add_btn)
                      except Exception as e:
                          log(f"Error clicking Add button via standard methods: {e}")
                      
                      # Wait for new box
                      box_spawned = False
                      try:
                          WebDriverWait(driver, 5).until(lambda d: len(d.find_elements(By.CSS_SELECTOR, "div[role='textbox']")) > old_count)
                          time.sleep(1)
                          box_spawned = True
                      except:
                          log("Warning: New box did not spawn after click.")

                      if not box_spawned:
                          log("CRITICAL: Failed to create new tweet box! Retrying click...")
                          try:
                              driver.execute_script("arguments[0].click();", add_btn)
                              time.sleep(3)
                              if len(driver.find_elements(By.CSS_SELECTOR, "div[role='textbox']")) > old_count:
                                  box_spawned = True
                                  log("Recovery successful! Box spawned on second try.")
                          except: pass

                      if not box_spawned:
                          log("ABORTING THREAD: Cannot spawn new box. Preventing overwrite in x_daemon.")
                          return {"status": "error", "message": f"Failed to spawn new tweet box for part {i+1}. Thread aborted."}
                  else:
                      log(f"CRITICAL Warning: 'Add' button NOT found for part {i+1}.")'''

    new_content = before + replacement + after
    with open('Scripts/x_daemon.py', 'w', encoding='utf-8') as f:
        f.write(new_content)
    print("SUCCESS")
else:
    print("MARKERS NOT FOUND")
