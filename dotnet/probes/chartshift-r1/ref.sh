#!/bin/bash
# ref.sh files... -> renders pdf + fodp/fods/fodt with 26.2 and dev into 262/ and dev/
for v in 262:/opt/libreoffice26.2 dev:/opt/libreofficedev26.8; do n=${v%%:*}; p=${v#*:}; mkdir -p $n
 for f in "$@"; do
  case $f in *.pptx|*.odp|*.fodp|*.ppt) fl=fodp;; *.xlsx|*.ods|*.fods|*.xls) fl=fods;; *) fl=fodt;; esac
  FONTCONFIG_FILE=/tmp/claude-0/fc-stock/fonts.conf $p/program/soffice --headless -env:UserInstallation=file:///tmp/claude-0/lo-profile/chartshift-$n --convert-to pdf --outdir $n $f >/dev/null 2>&1
  FONTCONFIG_FILE=/tmp/claude-0/fc-stock/fonts.conf $p/program/soffice --headless -env:UserInstallation=file:///tmp/claude-0/lo-profile/chartshift-$n --convert-to $fl --outdir $n $f >/dev/null 2>&1
 done; done
