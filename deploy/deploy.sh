set -euf -o pipefail

cd $(dirname $0)
cp -R ../web web
curl -H "Content-Type: application/vnd.bitballoon.v1.raw" "https://api.netlify.com/api/v1/sites/$netlify_site_id/files/spoilers.json?access_token=$netlify_access_token" -o web/spoilers.json