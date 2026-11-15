# BODEX Proxy

A Windows desktop proxy project intended to preserve the existing BODEX branding and shared activation contract while developing an optional system-wide Wintun tunnel.

## Project goals

- Preserve the existing BODEX Proxy UI and shared BODEX/HB Browser activation system.
- Keep the current ProxiFyre application-proxy mode available while the separate Wintun VPN mode is developed.
- Route device-wide IPv4/IPv6 traffic through the configured SOCKS5 proxy only after a fail-closed Windows network controller is implemented.
- Keep upstream license notices and comply with ProxiFyre's AGPL-3.0 requirements.

## Verified Wintun engine milestone

The standalone Windows x64 engine build is now verified:

- Pinned source: `xjasonlyu/tun2socks v2.7.0`.
- Toolchain: Go 1.26.3, matching the pinned source's `go.mod`.
- Windows amd64 build: successful on GitHub Actions / Windows Server 2022.
- Engine CLI version check: successful.
- SHA-256 generated and included with the artifact.
- MIT license notice included with the engine artifact.

Evidence and details:
- [Successful engine build run](https://github.com/bodex625-hash/BODEX-Proxy/actions/runs/43681294069)
- [Engine artifact verification and SHA-256](docs/WINTUN-ENGINE-VERIFICATION.md)
- [Wintun integration requirements and acceptance tests](docs/VPN-TUNNEL-INTEGRATION.md)

## Current integration status

The verified artifact is the tunnel engine only. It does not yet include BODEX's Windows service/controller, route and DNS management, firewall kill switch, crash recovery, or UI integration. It must not be described or distributed as a working system-wide VPN until those components are implemented and the acceptance tests pass.

The existing shared activation endpoint and activation-code contract are not changed by this Wintun engine build.

## Existing ProxiFyre baseline

The separate baseline workflow fetches official [wiresock/ProxiFyre](https://github.com/wiresock/proxifyre) source pinned to **v2.7.0**, including its NDISAPI submodule. That workflow remains distinct from the Wintun engine build.

## Known upstream limitation

The ProxiFyre baseline documents that fragmented IPv6 datagrams may pass through directly. Do not treat the existing application-proxy mode as a guarantee of zero network leaks; validate the exact configuration and traffic paths before production use.

## Licenses

- ProxiFyre: [GNU AGPL v3.0](https://github.com/wiresock/proxifyre/blob/v2.7.0/LICENSE).
- tun2socks: MIT; its license notice is included in the engine artifact.

Any distributed derivative must comply with the applicable upstream license terms, including required source availability and notices.
