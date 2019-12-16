set -euf -o pipefail

repo=git@github.com:schmich/mtg.cx
cd $(dirname $0)

openssl aes-256-cbc -iv $encrypted_ebc0ce15b791a71d_iv -K $encrypted_ebc0ce15b791a71d_key -in deploy_key.enc -out deploy_key -d
chmod 400 deploy_key
eval `ssh-agent -s`
ssh-add deploy_key

mkdir -p ~/.ssh
chmod 700 ~/.ssh
ssh-keyscan -H github.com >> ~/.ssh/known_hosts
chmod 600 ~/.ssh/known_hosts

spoilers=$(mktemp -d)
git clone --single-branch --branch gh-pages --depth 1 $repo $spoilers

../heroku_output/Spoilers $spoilers/spoilers.json $spoilers/spoilers.json

ghpages=$(mktemp -d)
git clone --single-branch --branch gh-pages --depth 3 $repo $ghpages

cd $ghpages
git reset --soft HEAD~1
git config user.name "Chris Schmich"
git config user.email "schmch@gmail.com"

cp $spoilers/spoilers.json ./spoilers.json
git commit -am "Update spoilers.json."

git push --force origin gh-pages