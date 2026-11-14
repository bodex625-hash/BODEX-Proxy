# BODEX Proxy

A Windows desktop proxy project intended to preserve the original ProxiFyre engine while introducing BODEX branding.

## Project goals

- Preserve the upstream proxy engine and routing behavior.
- Retain SOCKS5 support, TCP/UDP handling, per-application routing, DNS policy, and the upstream leak-mitigation behavior.
- Apply BODEX black/cyan visual identity to the Windows application and installer.
- Keep upstream license notices and publish corresponding modified source as required by AGPL-3.0.

## Upstream baseline

The initial reproducible Windows build workflow fetches the official [wiresock/ProxiFyre](https://github.com/wiresock/proxifyre) source pinned to **v2.7.0**, including its required NDISAPI submodule, then builds and tests the x64 baseline on a Windows 2022 runner.

## Current status

- Repository access is configured.
- A Windows x64 base-build workflow is committed at `.github/workflows/build-base.yml`.
- The workflow has not yet produced a verified artifact.
- **Important:** the current workflow builds the upstream ProxiFyre baseline; it is not yet the BODEX-rebranded application. BODEX UI, icon, product naming, installer branding, and final end-to-end verification remain required before calling this a finished BODEX release.

## Known upstream limitation

Upstream documents that fragmented IPv6 datagrams may pass through directly. Do not treat this project as a guarantee of zero network leaks; validate the exact configuration and traffic paths before production use.

## License

The upstream project is licensed under [GNU AGPL v3.0](https://github.com/wiresock/proxifyre/blob/v2.7.0/LICENSE). Any distributed derivative must comply with the license, including source availability and notices.
