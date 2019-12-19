set -euf -o pipefail

cd $(dirname $0)
cp -R ../web web
curl "https://mtg-cx.netlify.com/spoilers.json" -o web/spoilers.json