# Chapter 15: Troubleshooting

## Common Error Messages

### "Access Denied"

**Cause:** Insufficient clearance or permissions

**Check:**
1. Do you have "Read" or higher access to this notebook?
   - Go to Settings → Profile → Notebooks
2. Does your clearance dominate the entry's classification?
   - Entry requires: `SECRET / {Ops}`
   - Your clearance: `CONFIDENTIAL / {Ops}` ← Too low!

**Solution:**
```
Request clearance upgrade from your organization admin
OR
Request Read access from notebook owner
```

### "Clearance Insufficient for This Resource"

**Cause:** Classification mismatch

**Solution:**
```
Your clearance:     CONFIDENTIAL / {Ops}
Entry requires:     SECRET / {Ops}

1. You need SECRET level clearance (your org admin grants this)
2. Once granted, try again
3. Changes take effect within 5 minutes (or flush cache)
```

### "Entry Not Found"

**Cause:** Entry deleted, doesn't exist, or you lost access

**Check:**
```
1. Verify entry ID is correct
2. Check if entry was mirrored from external org (may have been deleted)
3. Try accessing via notebook instead of direct link
4. Check audit log for deletion event
```

### "Quota Exceeded"

**Cause:** You hit a usage limit

**Check:**
```
Quota Type              What to Do
──────────────────────────────────────
Notebooks exceeds limit  Request quota increase from admin
Entries per notebook     Archive old entries or split into 2 notebooks
Storage exceeds limit    Delete large entries or compress attachments
API calls per day        Reduce request frequency or batch operations
```

### "Network Error: Connection Refused"

**Cause:** Can't reach Cyber server

**Check:**
```
1. Is Cyber server online?
   curl https://cyber.company.com/api/health
   Should return: {"status":"ok"}

2. Is your internet working?
   ping 8.8.8.8

3. Is firewall blocking access?
   Check with IT/Network team

4. Is SSL certificate valid?
   curl -v https://cyber.company.com
   Look for SSL error messages
```

### "Invalid Token"

**Cause:** JWT token is expired, malformed, or revoked

**Check:**
```
1. Verify token is complete (should be 3 parts separated by dots)
2. Check if token is expired (ask admin or regenerate)
3. Verify spelling/copying of token
4. Check if token was revoked (Settings → API Tokens)
```

**Solution:**
```
Generate new token:
  Settings → API Tokens → [+ Generate New Token]
  Copy entire token string
  Update environment variable or config file
```

### "Job Processing Failed"

**Cause:** Background job error

**Check:**
```
1. What type of job failed?
   - EMBED_ENTRIES: Vector database issue
   - DISTILL_CLAIMS: NLP service unavailable
   - COMPARE_CLAIMS: Memory/timeout issue

2. Check if agent is running (Admin → Agents)
3. Review job logs (Admin → Jobs → [Job ID])
4. Check system status (Admin → Dashboard)
```

**Solution:**
```
1. Retry the job (Admin → Jobs → [Job] → Retry)
2. Wait and try again (may be temporary)
3. If persistent, contact admin or infrastructure team
```

---

## MCP/API Issues

### "Agent Not Responding"

**Symptom:** Claude can't access Cyber tools

**Check:**
```bash
# 1. Verify MCP server is running
ps aux | grep "notebook_client.mcp"

# 2. Check environment variables
echo $CYBER_URL
echo $CYBER_TOKEN

# 3. Test connection manually
curl -H "Authorization: Bearer $CYBER_TOKEN" \
  $CYBER_URL/api/health

# 4. Check Claude Desktop config
cat ~/.claude/claude_desktop_config.json
```

**Solution:**
```
1. Ensure notebook_client is installed:
   pip install notebook-client[mcp]

2. Verify credentials in .claude_desktop_config.json

3. Restart Claude Desktop:
   - Quit completely (Cmd+Q)
   - Wait 5 seconds
   - Reopen

4. If still failing, check logs:
   macOS: ~/Library/Logs/Claude/
```

### "Python Import Error: No module named 'notebook_client'"

**Solution:**
```bash
# Install/upgrade package
pip install --upgrade notebook-client[mcp]

# Verify installation
python3 -c "import notebook_client; print('OK')"

# Restart Claude Desktop
```

---

## Performance Issues

### "Searches are Slow"

**Cause:** Large index, slow network, or overloaded server

**Solution:**
```
1. Narrow search query (be more specific)
2. Filter by notebook or topic
3. Try again during off-peak hours
4. Report to admin if consistently slow
```

### "Notebook Loading is Slow"

**Cause:** Large notebook (many entries) or slow connection

**Solution:**
```
1. Use filters (status, topic, date range)
2. Paginate results (load 50 instead of all)
3. Close other tabs/apps consuming bandwidth
4. Check network speed (speedtest.net)
```

### "Agent Job Backlog Growing"

**Cause:** Agents can't keep up with demand

**Check:**
```
Admin → Dashboard → Job Queue

If backlog > 100:
  1. Check if agents are online
  2. See how many jobs in progress (may be slow)
  3. Check if agent hit resource limit (CPU/memory)
```

**Solution:**
```
Short-term:
  1. Add more agents (request from infrastructure)
  2. Reduce new entry creation (less load)

Long-term:
  1. Optimize job processing (faster model, better hardware)
  2. Scale horizontally (more agents)
```

---

## Subscription Issues

### "Subscription Sync Failing"

**Cause:** Network, permission, or classification issue

**Check:**
```
Admin → Subscriptions → [Problem subscription]

Look for error message:
  • "Connection refused" → Source org unreachable
  • "Unauthorized" → Lost access to source notebook
  • "Classification changed" → Bell-LaPadula violation
  • "Notebook deleted" → Source notebook no longer exists
```

**Solution:**
```
If "Classification changed":
  • Request clearance upgrade if needed
  • Or unsubscribe and resubscribe

If "Connection refused":
  • Check network connectivity
  • Verify source org is online

If "Unauthorized":
  • Request Read access from source notebook owner
  • Or generate new token
```

### "Entries Not Syncing"

**Cause:** Watermark stuck or sync paused

**Check:**
```
Watermark: Position 384
Source position: Position 392

Behind by 8 entries? Try:
  1. Click [Sync Now] to force immediate sync
  2. Wait 5 minutes
  3. Check subscription status for errors
```

---

## Account Issues

### "Can't Log In"

**Cause:** Wrong password, account locked, or system issue

**Solution:**
```
1. Verify you're using correct email
2. Try password reset (Settings → Account → Reset Password)
3. If account is locked, contact admin
4. If password reset doesn't work, contact support
```

### "Lost API Token"

**Cause:** Token wasn't saved

**Solution:**
```
Generate a new token:
  Settings → API Tokens → [+ Generate New Token]

Save it securely:
  • Environment variable: export CYBER_TOKEN="..."
  • Password manager: Save the token
  • .env file: Add to git .gitignore

DO NOT:
  • Commit token to code
  • Send token in messages/email
  • Share token with others
```

---

## Getting Help

### Where to Find Help

| Issue | Resource |
|-------|----------|
| How do I do X? | This manual + [Chapter relevant to your role] |
| API error | Chapter 11: MCP Integration Reference |
| UI question | Chapter 12: UI Reference |
| Security question | Chapter 2: Security Model |
| Configuration issue | This chapter (Troubleshooting) |
| Still stuck | Contact your Cyber admin or support@cyber.internal |

### Providing Information When Reporting Issues

```
When reporting a bug, include:
  1. What you were trying to do
  2. What actually happened
  3. Error message (if any)
  4. Steps to reproduce
  5. Your browser/version (if UI issue)
  6. Your clearance level (Settings → Profile)
  7. Relevant entry/notebook IDs
  8. Timestamp of issue occurrence

Example:
  "I tried to create an entry in my notebook at 2:30 PM today.
   I got error: 'Clearance insufficient for this resource'.
   My clearance is CONFIDENTIAL / {Ops}.
   The notebook is classified SECRET / {Ops}.
   Notebook ID: nb_xyz789"
```

---

**Last updated:** February 21, 2026
**Chapter version:** 1.0.0 (Beta)
**Platform Version:** 2.1.0
