# Quickstart

1. Copy the reference `.fsgg/` bundle into a product.
2. Add a `file` or `directory` entry to `.fsgg/controlled-imports.json`.
3. Add the exact destination `-text` rule to `.gitattributes`.
4. Compute the pin from raw bytes using
   `contracts/controlled-import-v1.md`.
5. Run `dotnet fsi .fsgg/controlled-imports.fsx -- --root .`.
6. If another gate needs to exempt a descendant, pass
   `--check-exemption <path>` and accept it only on zero exit.

See `docs/tutorials/controlled-imports.md` for a complete example.
