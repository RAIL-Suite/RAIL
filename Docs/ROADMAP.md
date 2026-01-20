# Roadmap: Planned Features & Known Gaps

This document tracks features that are planned but not yet implemented.

---

## High Priority

### 🧠 Conversation Memory (RailOrchestrator)

**Status:** Not Implemented

The current implementation lacks **chat history persistence**. Each message is treated independently without context from previous exchanges.

**What's missing:**
- Memory cache for multi-turn conversations
- Ability for the LLM to ask follow-up questions
- User responses feeding back into conversation context
- Session management (save/restore conversations)

**Impact:** Users cannot have back-and-forth dialogues with the AI. Each prompt is stateless.

**Technical approach:**
- Implement sliding window or summarization-based memory
- Store conversation history per session
- Inject history into system prompt or use native API context

---

### 🔐 Security Layer

**Status:** Not Implemented

See [SECURITY.md](../SECURITY.md) for full details.

**What's missing:**
- Client authentication (API keys, tokens)
- Command signing and verification
- Encrypted IPC communication
- Role-based access control
- Audit logging for AI-triggered actions

---

## Medium Priority

### 🏠 Local LLM Support (RailOrchestrator)

**Status:** Not Implemented

Currently only cloud-based LLMs are supported (Gemini, OpenAI, Anthropic). Local inference is not available.

**Planned integrations:**
- **Ollama** - Local model hosting with OpenAI-compatible API
- **FunctionGemma** - Specialized model for function calling (lightweight, fast)
- **LM Studio** - Desktop app for running local models
- Generic OpenAI-compatible endpoint support

**Benefits:**
- Privacy: Data stays local
- Cost: No API charges
- Offline: Works without internet
- Latency: Faster for local networks

---

### 📚 Document Workspace with RAG (RailOrchestrator)

**Status:** Not Implemented

Users should be able to create a **knowledge workspace** where they can:
- Upload documents (PDF, Word, Markdown, etc.)
- Auto-index with embeddings
- Ask questions about their documentation
- AI retrieves relevant context before answering

**What's missing:**
- Document ingestion pipeline
- Embedding generation (OpenAI, local models)
- Vector storage (ChromaDB integration exists but unused)
- RAG retrieval during chat
- Workspace management UI

---

## Low Priority / Future

### 🌐 Multi-Client Orchestration

Ability to connect to multiple client applications simultaneously and route commands to the correct one based on context.

### 📊 Execution Analytics

Dashboard showing:
- Commands executed per session
- Success/failure rates
- Latency metrics
- Most-used functions

### 🔌 Plugin System

Allow third-party extensions for:
- New LLM providers
- Custom command interceptors
- UI themes

---

## ⚠️ Technical Debt

### 🍝 [RailOrchestrator] GenerativePowerShell Custom Context 

**Status:** Needs Refactoring

**Location:** `Services/ReAct/ReActOrchestrator.cs` → `BuildInitialHistory()` method

**Problem:**

A **hardcoded custom context** was added specifically for `GenerativePowerShell` manifest type. This was a quick workaround to test applications that don't expose standard APIs but use **COM/OLE automation** (e.g., Microsoft Office desktop suite).

**What's wrong:**
- Spaghetti code: special case embedded in core logic
- Not extensible for other COM/OLE apps
- Manifest type detection is brittle
- Context injection should be configurable, not hardcoded

**Recommended fix:**
- Extract COM/OLE support into a dedicated adapter pattern
- Make custom context injection configurable per manifest
- Create proper abstraction for non-API automation scenarios
- Document COM/OLE integration as a separate feature

---

### 🔨 [RailStudio] BuildSolution Unstable 

**Status:** Unstable

**Problem:**

RailStudio currently only reliably handles **single EXE scanning**. The `BuildSolution` feature (for scanning entire `.sln` files) is unstable and may produce incomplete or incorrect results.

**What's wrong:**
- Multi-project solutions may not be fully parsed
- Dependency resolution between projects is unreliable
- Some edge cases cause crashes or hangs

**Recommended fix:**
- Review and stabilize solution parsing logic
- Add comprehensive error handling
- Consider using MSBuild APIs directly

---

### 🐌 UI Freezing on Save/Delete (RailStudio)

**Status:** Needs Investigation

**Problem:**

The UI becomes **slow/unresponsive** during certain Save and Delete operations.

**Suspected causes:**
- Synchronous I/O operations blocking UI thread
- Missing `async/await` on file operations
- Potential race conditions if multiple operations run concurrently
- No cancellation support for long-running operations

**What needs review:**
- All file I/O operations should be `async`
- UI thread should never block on disk operations
- Proper `IsBusy` state management
- Consider `SemaphoreSlim` for operation serialization
- Add cancellation tokens for interruptible operations

**Files to audit:**
- ManifestServicel Save/Delete commands

---

## Contributing

If you'd like to work on any of these features, please:
1. Open an issue to discuss approach
2. Reference this roadmap in your PR
3. Update this file when feature is completed


