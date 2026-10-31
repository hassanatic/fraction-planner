import { expect, test } from "@playwright/test";

test("deliver a fraction, miss the next and get a make-up session", async ({ page }) => {
  await page.goto("/");

  await page.getByLabel("Patient reference").fill("P-001");
  await page.getByLabel("Fractions").fill("5");
  await page.getByLabel("Start date").fill("2026-11-02");
  await page.getByRole("button", { name: "Create course" }).click();

  await expect(page.getByText("Delivered 0 of 5")).toBeVisible();
  await expect(page.getByRole("row")).toHaveCount(6); // header + 5 planned sessions

  await page.getByRole("button", { name: "Deliver fraction 1" }).click();
  await expect(page.getByText("Delivered 1 of 5")).toBeVisible();

  await page.getByRole("button", { name: "Mark fraction 2 missed" }).click();
  await expect(page.getByRole("row")).toHaveCount(7);
  await expect(page.getByRole("row").last()).toContainText("2026-11-09");
  await expect(page.getByRole("button", { name: "Deliver fraction 3" })).toBeVisible();
});

test("a rule break is shown to the user", async ({ page, request }) => {
  const created = await request.post("/api/courses", {
    data: { patientRef: "P-002", prescribedFractions: 2, startDate: "2026-11-02" },
  });
  const course = await created.json();
  const conflict = await request.post(`/api/courses/${course.id}/sessions/2/deliver`, { data: { date: null } });
  expect(conflict.status()).toBe(409);
  expect((await conflict.json()).error).toBe("session 1 comes first");

  await page.goto("/");
  await page.getByRole("button", { name: /P-002/ }).click();
  await expect(page.getByText("Delivered 0 of 2")).toBeVisible();
});
