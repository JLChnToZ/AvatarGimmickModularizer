# Avatar Gimmick Modularizer

[日本語版](README.JA.md)

This tool is for modularizing avatar accessories/gimmicks installation by packaging the originally "destructive" installation script into a "non-destructive/modularized" way on a separate avatar.

Beware this tool only supports installer scripts that adds things to your avatar, it doesn't know what to do if the script destructively remove things from your avatar.

Also, please make sure everything else in the dummy avatar is the same as the original avatar, otherwise the modularization may not work properly.

It depends on and will works with [Modular Avatar](https://modular-avatar.nadena.dev/).

## How to install?

0. If you haven't install Modular Avatar before, please add their package listings ([`https://vpm.nadena.dev/vpm.json`](vcc://vpm/addRepo?url=https://vpm.nadena.dev/vpm.json)) to your [VCC](https://vcc.docs.vrchat.com/), or [ALCOM](https://vrc-get.anatawa12.com/alcom/), [vrc-get](https://github.com/vrc-get/vrc-get) etc.
1. If you haven't install any of my packages before, please add [my package listings](https://xtlcdn.github.io/vpm/) ([`https://xtlcdn.github.io/vpm/index.json`](vcc://vpm/addRepo?url=https://xtlcdn.github.io/vpm/index.json)) as well.
2. Find and add the **Avatar Gimmick Modularizer** package to your avatar project, assume you already working on one.

## How to use it?

0. Open "Tools > JLChnToZ > Gimmick Modularizer"
1. Drop the avatar you are going to work on and click "Clone".
2. Install your desired accessories/gimmicks to the cloned dummy avatar.
3. After installation, click "Modularize" to pack the changes back to the original.
4. If you no longer want to re-install/modify the gimmick, you can safely delete the dummy avatar.

## License

[MIT](LICENSE)