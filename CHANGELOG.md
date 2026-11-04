# Changelog

## [0.3.0](https://github.com/lzocateli/PixelPointer/compare/v0.2.1...v0.3.0) (2026-11-03)


### ⚠ BREAKING CHANGES

* **actions:** configurar Settings > Environments > distribution com revisores obrigatórios e refs main/v* antes de ativar os workflows integrados. Não há secrets novos nesse ambiente. Se o plano GitHub não suportar a proteção, os publicadores devem permanecer bloqueados. Não confundir release (credenciais), quality (aceite de teste), distribution (autorização de divulgação) e production (verificação posterior).

### Adicionado

* **actions:** exija autorização antes da distribuição pública ([#51](https://github.com/lzocateli/PixelPointer/issues/51)) ([060f141](https://github.com/lzocateli/PixelPointer/commit/060f1415be310041ae22b3b8e7d779c4661fd736))
* **app:** mostre versão e atalhos no menu da tray ([#54](https://github.com/lzocateli/PixelPointer/issues/54)) ([1cc6c4b](https://github.com/lzocateli/PixelPointer/commit/1cc6c4b4c37bb4673e1c81e5ee2660da82bc0be4))

## [0.2.1](https://github.com/lzocateli/PixelPointer/compare/v0.2.0...v0.2.1) (2026-11-03)


### Corrigido

* **actions:** exija fonte atualizada antes de criar releases ([#49](https://github.com/lzocateli/PixelPointer/issues/49)) ([84bc959](https://github.com/lzocateli/PixelPointer/commit/84bc959b59ab42f7a80182d80fbd76b15964333e))

## [0.2.0](https://github.com/lzocateli/PixelPointer/compare/v0.1.2...v0.2.0) (2026-11-03)


### Adicionado

* **app:** adicione gravação de tela em GIF com Ctrl+5 ([#39](https://github.com/lzocateli/PixelPointer/issues/39)) ([8ad15b2](https://github.com/lzocateli/PixelPointer/commit/8ad15b258e39850d7add761248e590a38954ec60))
* **app:** adicione Live Zoom no Windows com Ctrl+4 ([#41](https://github.com/lzocateli/PixelPointer/issues/41)) ([4e02a2d](https://github.com/lzocateli/PixelPointer/commit/4e02a2dd2fbad6a3105fec8c943244d06141a224))
* **app:** adicione Live Zoom no X11 como lupa em janela ([#42](https://github.com/lzocateli/PixelPointer/issues/42)) ([f7df20e](https://github.com/lzocateli/PixelPointer/commit/f7df20ece05d908e286bc00b13ff7497f8830c7b))
* **app:** adicione preparação explícita de primeiro uso ([#26](https://github.com/lzocateli/PixelPointer/issues/26)) ([3d114a4](https://github.com/lzocateli/PixelPointer/commit/3d114a45a035592f192d60f337419f32fd29765a))
* **app:** adicione recorte de tela com Ctrl+6 ([#37](https://github.com/lzocateli/PixelPointer/issues/37)) ([698ff85](https://github.com/lzocateli/PixelPointer/commit/698ff85bda403dad9efc9dc006364475fd36f47b))
* **app:** adicione timer de intervalo com Ctrl+3 ([#36](https://github.com/lzocateli/PixelPointer/issues/36)) ([d662f56](https://github.com/lzocateli/PixelPointer/commit/d662f5628baa35d0f02d8a6d10fce3aacb824919))
* **app:** aplique preferências do settings.json nas sobreposições ([#13](https://github.com/lzocateli/PixelPointer/issues/13)) ([00e8238](https://github.com/lzocateli/PixelPointer/commit/00e82382f4ef9d023c123100ff1e13a7f20e02a0))
* **capture:** capture no Wayland por ScreenCast e PipeWire sob demanda ([#16](https://github.com/lzocateli/PixelPointer/issues/16)) ([1316dd0](https://github.com/lzocateli/PixelPointer/commit/1316dd077335edec968a8575a0e256ebe29a9e52))
* **capture:** grave a tela no Wayland por ScreenCast contínuo ([#40](https://github.com/lzocateli/PixelPointer/issues/40)) ([afc2142](https://github.com/lzocateli/PixelPointer/commit/afc214252152b123af321a4d8b43a8442ae59793))
* **linux:** adicione Live Zoom Wayland e filtre auto-repeat X11 ([#45](https://github.com/lzocateli/PixelPointer/issues/45)) ([9f0fcee](https://github.com/lzocateli/PixelPointer/commit/9f0fcee0b57b1f0f1ac50be0ad02c2bd3aed89f1))
* **linux:** adicione Live Zoom Wayland e filtre auto-repeat X11 ([#46](https://github.com/lzocateli/PixelPointer/issues/46)) ([3e833ad](https://github.com/lzocateli/PixelPointer/commit/3e833ad83934a1b80434dd9259db5b0f260c76dc))
* **macos:** adicione Live Zoom com ScreenCaptureKit ([#44](https://github.com/lzocateli/PixelPointer/issues/44)) ([a03bfbc](https://github.com/lzocateli/PixelPointer/commit/a03bfbc96842bf5bfa2a025fc7efb1152c2e3450))
* **macos:** use ScreenCaptureKit na captura estática ([#47](https://github.com/lzocateli/PixelPointer/issues/47)) ([1911d66](https://github.com/lzocateli/PixelPointer/commit/1911d66d285406081388a7187853564b756e3578))
* **overlay:** abra a sobreposição no monitor do cursor ([#38](https://github.com/lzocateli/PixelPointer/issues/38)) ([6916222](https://github.com/lzocateli/PixelPointer/commit/6916222f433c673dd09d3405929bdbfb5d2eb276))
* **overlay:** adicione caneta de desfoque ([#33](https://github.com/lzocateli/PixelPointer/issues/33)) ([dcb3e7b](https://github.com/lzocateli/PixelPointer/commit/dcb3e7be4ce5be10b13fb94ead05aed698dead94))
* **overlay:** adicione linha, seta, retângulo e elipse ([#30](https://github.com/lzocateli/PixelPointer/issues/30)) ([98d76dd](https://github.com/lzocateli/PixelPointer/commit/98d76dd1b9b1395681db95d379f738eb1e1e2d17))
* **overlay:** adicione marca-texto translúcido ([#32](https://github.com/lzocateli/PixelPointer/issues/32)) ([af46dae](https://github.com/lzocateli/PixelPointer/commit/af46dae996607c1318d38f1d03d424387036a005))
* **overlay:** adicione texto na sobreposição ([#35](https://github.com/lzocateli/PixelPointer/issues/35)) ([fcfe508](https://github.com/lzocateli/PixelPointer/commit/fcfe508d04b0b06f384d5aa558fa5c40cc23cefb))
* **overlay:** copie a imagem para a área de transferência com Ctrl+C ([#34](https://github.com/lzocateli/PixelPointer/issues/34)) ([75d8f02](https://github.com/lzocateli/PixelPointer/commit/75d8f027ffe4a140be25451ab6ba7b29edc2ac4f))
* **rendering:** adicione renderização OpenGL opcional com retorno à CPU ([#15](https://github.com/lzocateli/PixelPointer/issues/15)) ([3f805fe](https://github.com/lzocateli/PixelPointer/commit/3f805fe2fadf455f814cf423ac33e10222aaf9c9))


### Corrigido

* **actions:** publique previews como GitHub Pre-release ([#18](https://github.com/lzocateli/PixelPointer/issues/18)) ([cdd125e](https://github.com/lzocateli/PixelPointer/commit/cdd125e17e03691970a9293eed8f870be3dbab12))
* **capture:** atualize a fonte após recuperação do ScreenCast ([#25](https://github.com/lzocateli/PixelPointer/issues/25)) ([6b35cfe](https://github.com/lzocateli/PixelPointer/commit/6b35cfe5da508e7e8c29f41af54cc1b477c4ca9f))
* **capture:** trate falhas de Screenshot sem abortar a CLI ([#23](https://github.com/lzocateli/PixelPointer/issues/23)) ([05b0c40](https://github.com/lzocateli/PixelPointer/commit/05b0c4018de3127466807c9c2eb50cf4dbf29d1a))
* **capture:** use o portal Screenshot no GNOME ([#28](https://github.com/lzocateli/PixelPointer/issues/28)) ([8049f13](https://github.com/lzocateli/PixelPointer/commit/8049f133d25c9d82907f70b57f70bcfe0e173a03))
* **linux:** corrija falhas do aceite da preview.16 ([#27](https://github.com/lzocateli/PixelPointer/issues/27)) ([65b91b5](https://github.com/lzocateli/PixelPointer/commit/65b91b5295a131152b244ba6ed74e5152517e1d9))
* **overlay:** restaure fullscreen no Wayland com HiDPI ([#24](https://github.com/lzocateli/PixelPointer/issues/24)) ([8f4028d](https://github.com/lzocateli/PixelPointer/commit/8f4028d7ebc6970abd1072913c37dcb843765c78))


### Desempenho

* **capture:** agrupe consultas e valide o daemon Wayland ([#21](https://github.com/lzocateli/PixelPointer/issues/21)) ([df252e6](https://github.com/lzocateli/PixelPointer/commit/df252e66ad2d360b4ac402fa8b9a7decd0c6908a))

## [0.1.2](https://github.com/lzocateli/PixelPointer/compare/v0.1.1...v0.1.2) (2026-10-31)


### Corrigido

* **packaging:** instale o ícone SVG em todas as plataformas ([#8](https://github.com/lzocateli/PixelPointer/issues/8)) ([f3ff01e](https://github.com/lzocateli/PixelPointer/commit/f3ff01e5a20687a1e7eacd070c0ad4e91e2266f5))

## [0.1.1](https://github.com/lzocateli/PixelPointer/compare/v0.1.0...v0.1.1) (2026-10-30)


### Corrigido

* **linux:** corrija integração X11 e ciclo de vida do desktop ([#4](https://github.com/lzocateli/PixelPointer/issues/4)) ([456d1df](https://github.com/lzocateli/PixelPointer/commit/456d1df4c711386f3bfcbbd0e179b58db70febc3))

## [0.1.0](https://github.com/lzocateli/PixelPointer/compare/v0.0.1...v0.1.0) (2026-10-30)


### Adicionado

* **app:** zoom e desenho de tela com atalhos globais e entrega multiplataforma ([bd5553b](https://github.com/lzocateli/PixelPointer/commit/bd5553b72416b94c984f591c0cb0baf44ef632b8))

## Changelog
