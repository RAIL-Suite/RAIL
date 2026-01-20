# Security Policy

## ⚠️ Security Warnings

### This software is NOT production-ready

RAIL Protocol is a **research/development project** with known security limitations. Please read carefully before use.

---

## Known Security Issues

### 1. Memory Injection

Some components use **memory injection** techniques to control legacy applications:

- We hook into running processes to intercept/modify behavior
- This bypasses normal application security boundaries
- **Risk:** Any process with the DLL loaded can be controlled remotely

**Recommendation:** Only use on trusted, isolated machines (development/demo environments).

---

### 2. No Authentication Layer

The Named Pipe communication **has no authentication**:

- Any local process can connect to `RailHost` pipe
- Commands are executed without identity verification
- No encryption on IPC channel

**What's missing:**
- [ ] Client authentication (API keys, tokens)
- [ ] Command signing/verification
- [ ] TLS/encryption for IPC
- [ ] Role-based access control
- [ ] Audit logging

**Recommendation:** Do NOT expose this on networked/multi-user systems.

---

### 3. Arbitrary Code Execution by Design

The AI can call **any registered method** on connected applications. This is intentional for agentic control but dangerous if:

- Malicious prompts are injected
- Untrusted users have access to the chat interface
- Connected applications have destructive capabilities

---

## Recommended Use Cases

| ✅ Safe | ❌ Unsafe |
|---------|----------|
| Local development machine | Production servers |
| Demo/POC environments | Multi-user systems |
| Isolated test VMs | Internet-facing deployments |
| Personal automation | Handling sensitive data |

---

## Reporting Vulnerabilities

If you discover a security issue, please open a GitHub issue or contact the maintainers directly.

---

## Roadmap: Security Improvements

Future versions may include:

1. **Authentication** - API key validation for client connections
2. **Encryption** - TLS for Named Pipe communication
3. **Sandboxing** - Method allowlists per connected app
4. **Audit Logging** - Track all AI-triggered actions

---

> **Disclaimer:** Use at your own risk. The authors are not responsible for any damage caused by using this software in inappropriate environments.


