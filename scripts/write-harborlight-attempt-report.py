#!/usr/bin/env python3
"""Write a visual PDF for the recorded Harborlight free-generation attempt."""

import json
import sys
from pathlib import Path

from fpdf import FPDF


def paragraph(pdf: FPDF, text: str, height: int = 6) -> None:
    pdf.set_x(pdf.l_margin)
    pdf.multi_cell(
        pdf.w - pdf.l_margin - pdf.r_margin,
        height,
        text.encode("latin-1", "replace").decode("latin-1"),
        new_x="LMARGIN",
        new_y="NEXT",
    )


def heading(pdf: FPDF, text: str) -> None:
    pdf.set_font("Helvetica", "B", 15)
    paragraph(pdf, text, 8)
    pdf.set_font("Helvetica", size=10)


def add_image_page(pdf: FPDF, title: str, path: Path, notice: str) -> None:
    pdf.add_page()
    heading(pdf, title)
    paragraph(pdf, notice)
    pdf.ln(3)
    available_width = pdf.w - pdf.l_margin - pdf.r_margin
    available_height = pdf.h - pdf.get_y() - pdf.b_margin
    pdf.image(str(path), x=pdf.l_margin, y=pdf.get_y(), w=available_width, h=available_height, keep_aspect_ratio=True)


def main() -> None:
    if len(sys.argv) != 2:
        raise SystemExit("usage: write-harborlight-attempt-report.py OUTPUT")

    root = Path(__file__).resolve().parents[1]
    attempt = root / "assets/alpha-prototypes/creative-academy/harborlight"
    evidence = json.loads((attempt / "HV-001-evidence.json").read_text(encoding="utf-8"))
    output = Path(sys.argv[1])

    pdf = FPDF(format="Letter")
    pdf.set_auto_page_break(auto=True, margin=16)
    pdf.set_margins(16, 16, 16)
    pdf.add_page()
    heading(pdf, "HARBORLIGHT FREE-GENERATION ATTEMPT")
    paragraph(pdf, "Attempt HV-001 | 2026-10-07 | GENERATED_PENDING_HUMAN_REVIEW")
    paragraph(pdf, "One original image was generated without buying a subscription and without sending Academy reference image files. The provider capability did not expose its model, job ID, or price. Those facts remain UNREPORTED_BY_PROVIDER; the attempt is not relabeled as OpenAI and its cost is not called zero.")
    heading(pdf, "Brief")
    brief = evidence["brief"]
    paragraph(pdf, f"{brief['advertiser']} | {brief['city']}, {brief['market']} | {brief['niche']} | {brief['inventoryProductId']}")
    paragraph(pdf, brief["objective"])
    heading(pdf, "Automatic retrieval")
    for reference in evidence["retrieval"]["references"]:
        paragraph(pdf, f"{reference['referenceId']} - {reference['reason']}. LEARN: {reference['learn']}. DO NOT COPY: {reference['doNotCopy']}.")
    paragraph(pdf, "References were not manually selected. Reference image files sent to provider: No.")
    heading(pdf, "Provider and usage")
    provider = evidence["provider"]
    paragraph(pdf, f"Provider: {provider['name']}. Model: {provider['model']}. Job ID: {provider['jobId']}. Attempts: {provider['attempts']}. Usage: {provider['usage']}. Cost: {provider['costStatus']}. New subscription: No.")
    heading(pdf, "Integrity")
    paragraph(pdf, f"Original SHA-256: {evidence['original']['sha256']}")
    paragraph(pdf, f"Adapted preview SHA-256: {evidence['adaptation']['sha256']}")
    paragraph(pdf, "Campaign ready: false. Delivery: NOT_SENT. Regression: BASELINE_NOT_RECORDED. Creative Academy: OPEN. Advertising Real Estate: OPEN. Hosted acceptance: UNCLAIMED.")

    add_image_page(
        pdf,
        "FINISHED ORIGINAL - PENDING HUMAN REVIEW",
        attempt / "HV-001-original.jpg",
        "1280x720 generated original. Human visual quality, Panama-market authenticity, and pixel originality have not been accepted.",
    )
    add_image_page(
        pdf,
        "ARE-P01 TEST RECOMPOSITION - NOT DELIVERED",
        attempt / "HV-001-adapted-preview.png",
        "1920x1080 test preview. LEFT_VERTICAL contains the original brand panel at 320x1080. BOTTOM_FULL is a purpose-built 1280x180 banner with the complete headline. The center is protected creator content. This preview was not delivered.",
    )

    pdf.add_page()
    heading(pdf, "Workflow trace")
    for step in evidence["workflowTrace"]:
        paragraph(pdf, f"{step['sequence']}. {step['step']}: {step['status']}. {step['evidence']}")
    heading(pdf, "Pixel similarity")
    similarity = evidence["pixelSimilarity"]
    paragraph(pdf, f"Status {similarity['status']}. Judgment {similarity['judgment']}. Visual grade {similarity['visualGrade']}.")
    paragraph(pdf, similarity["notice"])
    for distance in similarity["references"]:
        paragraph(pdf, f"{distance['referenceId']}: mean absolute error {distance['meanAbsoluteError']}, average-hash distance {distance['averageHashDistance']} of 64.")
    heading(pdf, "HV-4 review sheet")
    sheet = evidence["reviewSheet"]
    paragraph(pdf, f"Sheet {sheet['status']}. Human review {sheet['humanReview']}. Regression {sheet['regression']}. Campaign ready: false. Delivery: NOT_SENT.")
    paragraph(pdf, sheet["notice"])
    for attribute in sheet["attributes"]:
        paragraph(pdf, f"{attribute['name']}: {attribute['grade']}")
    for check in sheet["copyChecks"]:
        paragraph(pdf, f"Copy check {check['name']}: {check['status']}")
    for question in sheet["questions"]:
        paragraph(pdf, f"{question['name']}: {question['status']}")
    heading(pdf, "Review required")
    for note in evidence["reviewNotes"]:
        paragraph(pdf, "- " + note)
    heading(pdf, "Current gates")
    checks = evidence["checks"]
    for key in [
        "textIdentityOriginality",
        "pixelSimilarity",
        "deterministicImageValidation",
        "visualQa",
        "humanReview",
        "regression",
    ]:
        paragraph(pdf, f"{key}: {checks[key]}")
    paragraph(pdf, "The attempt advances the generation and evidence phases. It does not complete human review, production acceptance, either parent amendment, delivery, hosted acceptance, or the regression baseline.")

    output.parent.mkdir(parents=True, exist_ok=True)
    pdf.output(str(output))


if __name__ == "__main__":
    main()
