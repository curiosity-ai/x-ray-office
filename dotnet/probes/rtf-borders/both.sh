#!/bin/bash
# usage: both.sh DIR  -- renders every probe file in DIR with the daily (ref/) and with our CLI (ours/)
set -u
D=$1
export PATH=$HOME/.dotnet:$PATH DOTNET_ROOT=$HOME/.dotnet
CLI=/home/user/xo-frames/dotnet/tools/XRay.Office.Cli/bin/Debug/net10.0/linux-x64/XRay.Office.Cli
SO=/opt/libreofficedev26.8/program/soffice
mkdir -p $D/ref $D/ours
for f in $D/*.rtf $D/*.docx $D/*.odt $D/*.doc; do
  [ -e "$f" ] || continue
  s=$(basename "$f"); s=${s%.*}
  if [ "${NOREF:-0}" != 1 ] && [ ! -e $D/ref/$s.pdf -o "$f" -nt $D/ref/$s.pdf ]; then
    mkdir -p $D/ref/tmp-$s
    FONTCONFIG_FILE=/tmp/claude-0/fc-stock/fonts.conf timeout 120 $SO -env:UserInstallation=file:///tmp/claude-0/lo-profile/wordgaps --headless --convert-to pdf --outdir $D/ref/tmp-$s "$f" >/dev/null 2>&1
    mv $D/ref/tmp-$s/$s.pdf $D/ref/$s.pdf 2>/dev/null || echo "REF FAILED $s"; rmdir $D/ref/tmp-$s 2>/dev/null
  fi
  $CLI render --quiet --outdir $D/ours "$f" || echo "OURS FAILED $s"
  [ -e $D/ours/$s.pdf ] || echo "OURS MISSING $s"
done
