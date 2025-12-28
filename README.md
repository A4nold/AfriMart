## Oracle & Market Resolution

AfriMart uses a modular oracle architecture to resolve prediction markets across sports, crypto prices, and African FX rates.

Resolution logic is intentionally separated from core market mechanics, allowing different oracle strategies (optimistic, API-based, committee-based, or hybrid) to be plugged in over time without changing the on-chain market program.

📄 **Detailed design and rationale:**  
[Oracle Resolution Architecture](./docs/architecture/Oracle_Resolution_Architecture.pdf)
*Planned to build, manual resolution with authority key as a signer for now.

AfriMart prioritizes transparency, dispute-resistance, and extensibility in market resolution.  
All resolutions ultimately finalize on-chain, with clear audit trails and deterministic outcomes.



## Developer Documentation

- 📄 [Jan 2025 Technical Summary](docs/progress/2025-01-technical-summary.pdf) 
