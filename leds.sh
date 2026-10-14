#!/usr/bin/env bash
# Liga e desliga os LEDs do water cooler (bomba + 2 ventoinhas do radiador)
# pelo controlador ASUS Aura USB da PRIME B550M-A, usando a CLI do OpenRGB.
#
# Uso:
#   ./leds.sh desligar
#   ./leds.sh ligar [RRGGBB]     (padrão: FFFFFF)
#   ./leds.sh arco-iris
#
# As 3 ventoinhas do gabinete não passam pela placa; este script não as alcança.

set -euo pipefail

OPENRGB="${OPENRGB:-/d/Programas/OpenRGB/OpenRGB.exe}"
DISPOSITIVO="${LEDS_DISPOSITIVO:-ASUS PRIME B550M-A}"

openrgb() {
    # A interface do OpenRGB está aberta? Ela segura o USB e a CLI briga com ela.
    if tasklist //FI "IMAGENAME eq OpenRGB.exe" 2>/dev/null | grep -qi openrgb.exe; then
        echo "Feche a interface do OpenRGB antes (ou use o plano 2, com o servidor SDK)." >&2
        exit 1
    fi
    "$OPENRGB" --noautoconnect -d "$DISPOSITIVO" "$@"
}

cor_valida() {
    [[ "$1" =~ ^[0-9A-Fa-f]{6}$ ]]
}

case "${1:-}" in
    desligar)
        openrgb -m off
        ;;
    ligar)
        cor="${2:-FFFFFF}"
        cor_valida "$cor" || { echo "Cor inválida: $cor (use RRGGBB)" >&2; exit 2; }
        openrgb -m static -c "$cor"
        ;;
    arco-iris)
        openrgb -m "spectrum cycle"
        ;;
    *)
        echo "Uso: $0 {desligar|ligar [RRGGBB]|arco-iris}" >&2
        exit 2
        ;;
esac
