# AfriMart — Solana Prediction Markets (MVP)

> **Status:** Active development (MVP complete, client in progress)  
> **Founder:** Solo builder  
> **Chain:** Solana (Anchor v2)  
> **Backend:** .NET 8 microservices, PostgreSQL  
> **Stage:** End-to-end trading live, portfolio reads stable, wallet auth in progress

---

## Overview

AfriMart is a **Solana-based prediction market** built around a **YES / NO Constant Product Market Maker (CPMM)** model.

The system combines:
- On-chain programs for trustless trading and settlement
- Off-chain services for orchestration, safety, and UX
- A clear separation between execution, read models, and authentication

The current MVP focuses on **correctness, determinism, and safety** before expanding into liquidity incentives and user-facing clients.

---

## Core Features (MVP)

- Create prediction markets on Solana
- Buy / sell YES or NO shares using CPMM math
- Slippage-safe quoting (off-chain, deterministic)
- Market resolution + pro-rata payouts
- Idempotent execution layer (retry-safe)
- Portfolio read service (cached from chain)
- Wallet-based auth backend (Phantom-ready)

---

## Architecture

### On-Chain (Solana / Anchor v2)

**Accounts (PDAs):**
- `MarketV2`
  - Market metadata
  - YES / NO pools
  - Total outstanding shares
  - Market status + winning outcome
- `PositionV2`
  - One per (market, user)
  - YES / NO share balances
  - Claimed flag

**Instructions:**
- `create_market_cpmm`
- `buy_shares`
- `sell_shares`
- `resolve_market`
- `claim_winnings_v2`

The on-chain program is **fully tested end-to-end** and treated as the **source of truth**.

---

### Off-Chain Services

#### 1. BlockchainService

Stateless Solana adapter responsible for:
- PDA derivation
- Instruction + transaction construction
- Send & confirm logic
- Decoding on-chain accounts:
  - `MarketV2`
  - `PositionV2`

No business logic lives here — this service only speaks Solana.

---

#### 2. MarketService (Executor & Orchestration)

This is the **core application service**.

Responsibilities:
- Validate commands
- Enforce market rules
- Execute on-chain actions
- Handle retries safely
- Persist action history

**Key components:**
- **MarketActionExecutor**
  - Idempotency keys
  - Retry-safe execution
  - Anchor error classification
- **MarketAction ledger**
  - Request / response JSON
  - Transaction signature
  - Error metadata
- **CPMM Quote Engine**
  - Pure math (no RPC calls)
  - Used for:
    - Quote buy
    - Quote sell
    - Slippage-safe UX

The quote engine eliminated recurring “slippage exceeded” errors by aligning user inputs with actual pool math.

---

#### 3. PortfolioService (Read-only)

PortfolioService is **read-only** and **non-authoritative**.

- Uses the same database context
- Never sends transactions
- Never mutates on-chain state

**Entity: `UserMarketPosition`**
- Cached snapshot of on-chain `PositionV2`
- Updated only after confirmed trades or syncs

**Computed fields exposed via API:**
- Has exposure
- Exposure side (YES / NO / MIXED)
- Market status
- Can claim winnings
- Open / resolved filtering

All queries are **fully EF Core translatable** (no client-side evaluation).

---

#### 4. AuthService

JWT-based authentication service.

**Current features:**
- Email / password login (legacy)
- Roles + claims
- Refresh tokens

**Wallet Auth (in progress):**
- Phantom-compatible challenge–response login
- Ed25519 signature verification
- Nonce stored in DB
- Users auto-created on first wallet login
- Wallet public key linked to user
- JWT issued after successful verification

Backend support is complete; client testing will follow.

---

## CPMM Model (How Trading Works)

Each market maintains **two virtual pools**:
- `yes_pool`
- `no_pool`

**Invariant:**
yes_pool * no_pool = k

### Liquidity
- Initial liquidity is house-funded
- No external LPs yet (intentional for MVP)

### Buying Shares
- User pays collateral
- Fee taken on input
- Net collateral moves the opposite pool
- Shares minted from target pool

### Selling Shares
- User burns shares
- CPMM computes gross collateral out
- Fee taken on output
- Net collateral transferred to user
- Fee stays in the vault (improves solvency)

### Resolution & Payout
- Market resolved by authority
- Vault paid out pro-rata to winning positions
- Positions marked claimed on-chain

The model is:
- Deterministic
- Slippage-aware
- Easy to quote off-chain
- Safe for early-stage liquidity

---

## What’s Done vs Deferred

### Completed
- End-to-end trading
- Quote engine
- Idempotent execution
- Portfolio reads
- Market + position decoding
- Initial sync endpoint
- Wallet auth backend prep

### Deferred (Intentionally)
- Frontend client
- Phantom auth testing
- LP incentives
- User as fee payer
- Advanced background indexing
- UX polish

---

## Next Steps

### Short-term
1. Scaffold production-ready client
2. Add Phantom wallet sign-in
3. Verify wallet auth end-to-end
4. Display portfolio view

### Medium-term
- Move fee payer to user
- Harden sync logic
- Prepare public demo

---

## Philosophy

This project prioritizes:
- Correctness over speed
- Determinism over cleverness
- Infrastructure before UX

The goal is to prove the system is:

> Safe, composable, and production-capable — before scaling usage.

---

## License
Open Source

📄 **Detailed design and rationale:**  
[Oracle Resolution Architecture](./docs/architecture/Oracle_Resolution_Architecture.pdf)
*Planned to build, manual resolution with authority key as a signer for now.

AfriMart prioritizes transparency, dispute-resistance, and extensibility in market resolution.  
All resolutions ultimately finalize on-chain, with clear audit trails and deterministic outcomes.



## Developer Documentation

- 📄 [Jan 2025 Technical Summary](docs/progress/2025-01-technical-summary.pdf) 
