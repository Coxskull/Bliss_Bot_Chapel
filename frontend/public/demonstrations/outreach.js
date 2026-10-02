const slug = location.pathname.split("/").filter(Boolean).pop();
const subject = document.querySelector("#subject");
const body = document.querySelector("#body");
const benefits = document.querySelector("#benefits");
const open = document.querySelector("#open");
const fine = document.querySelector("#fine");

async function load() {
  const response = await fetch("/api/demonstrations/" + slug);
  if (!response.ok) {
    subject.textContent = "The demonstration message is not ready";
    body.textContent = "Produce it from the Source Media library first. This preview is not emailed.";
    return;
  }
  const page = await response.json();
  document.title = page.subject;
  subject.textContent = "Subject: " + page.subject;
  body.textContent = page.personalizationAllowed && page.decisionMakerName
    ? "We're Alpha, a podcast advertising platform. The recorded public name for " + page.businessName
      + " is " + page.decisionMakerName + ", " + page.decisionMakerRole + ". Confidence is " + page.decisionMakerStatus
      + ". These private concepts are not a live campaign, and this preview has not been sent."
    : "We're Alpha, a podcast advertising platform that helps local businesses in " + page.market
      + " get more exposure by placing ads inside short videos people in that community can watch. These are private concepts for "
      + page.businessName + ". They are not a live campaign.";
  ["Reach more local customers", "Be part of short video in " + page.market, "Build awareness in the community", "Drive customers to the store, website, or offers"]
    .forEach(item => {
      const line = document.createElement("li");
      line.textContent = item;
      benefits.append(line);
    });
  open.href = "/demonstrations/" + page.slug;
  fine.textContent = "This is a private demonstration preview created for " + page.businessName
    + " for illustrative purposes only. Delivery status: " + page.delivery + ". " + page.disclosure;
}

load();
