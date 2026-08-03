using Microsoft.EntityFrameworkCore;
using ServiceRequest.Application.Requests;
using ServiceRequest.Domain.Entities;
using ServiceRequest.Domain.Enums;
using ServiceRequest.Infrastructure.Data;

namespace ServiceRequest.Infrastructure.Seed;

public static class DevelopmentDataSeeder
{
    public static async Task SeedAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        await SeedCategoriesAsync(dbContext, cancellationToken);
        await SeedRequestsAsync(dbContext, cancellationToken);
    }

    private static async Task SeedCategoriesAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var definitions = new[]
        {
            ("Hardware",         "Desktop computers, printers, monitors, keyboards, and other physical equipment", true),
            ("Software",         "Application installation, licensing, updates, and operating system issues",      true),
            ("Network",          "Internet connectivity, VPN, Wi-Fi, and network access issues",                  true),
            ("Access & Accounts","User accounts, passwords, shared drive permissions, and access requests",       true),
            ("Other",            "General requests that do not fit into a more specific category",                 true),
            // Inactive — demonstrates historical data and deactivated-category behavior.
            ("Facilities",       "Office equipment, HVAC, lighting, and building-related requests",               false),
        };

        foreach (var (name, description, isActive) in definitions)
        {
            if (await dbContext.RequestCategories.AnyAsync(c => c.Name == name, cancellationToken))
            {
                continue;
            }

            var category = new RequestCategory(name, description);

            if (!isActive)
            {
                category.SetActiveState(false);
            }

            dbContext.RequestCategories.Add(category);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedRequestsAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (await dbContext.SupportRequests.AnyAsync(cancellationToken))
        {
            return;
        }

        var admin = await dbContext.Users.SingleAsync(u => u.Username == "admin", cancellationToken);
        var agent = await dbContext.Users.SingleAsync(u => u.Username == "agent", cancellationToken);
        var agent2 = await dbContext.Users.SingleAsync(u => u.Username == "agent2", cancellationToken);
        var employee = await dbContext.Users.SingleAsync(u => u.Username == "employee", cancellationToken);
        var employee2 = await dbContext.Users.SingleAsync(u => u.Username == "employee2", cancellationToken);

        var hardware = await dbContext.RequestCategories.SingleAsync(c => c.Name == "Hardware", cancellationToken);
        var software = await dbContext.RequestCategories.SingleAsync(c => c.Name == "Software", cancellationToken);
        var network = await dbContext.RequestCategories.SingleAsync(c => c.Name == "Network", cancellationToken);
        var access = await dbContext.RequestCategories.SingleAsync(c => c.Name == "Access & Accounts", cancellationToken);
        var other = await dbContext.RequestCategories.SingleAsync(c => c.Name == "Other", cancellationToken);

        var requests = CreateRequests(agent, agent2, employee, employee2, hardware, software, network, access, other);
        dbContext.SupportRequests.AddRange(requests);
        await dbContext.SaveChangesAsync(cancellationToken);

        var histories = CreateHistoryEntries(requests, admin, agent, agent2, employee, employee2, software, other);
        var comments = CreateComments(requests, agent, employee);

        dbContext.RequestHistory.AddRange(histories);
        dbContext.RequestComments.AddRange(comments);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static SupportRequest[] CreateRequests(
        ApplicationUser agent,
        ApplicationUser agent2,
        ApplicationUser employee,
        ApplicationUser employee2,
        RequestCategory hardware,
        RequestCategory software,
        RequestCategory network,
        RequestCategory access,
        RequestCategory other)
    {
        // r0 — InProgress, agent assigned (Critical)
        var r0 = new SupportRequest(
            "Printer losing network connection every 15 minutes",
            "The HP LaserJet M404n in the main office area keeps losing its network connection " +
                "roughly every 15 minutes. Other devices on the same switch are unaffected. " +
                "Power cycling the printer restores connectivity temporarily.",
            RequestPriority.Critical, hardware, employee);
        r0.AssignTo(agent);
        r0.ChangeStatus(RequestStatus.InProgress);

        // r1 — WaitingForUser, agent assigned (High)
        var r1 = new SupportRequest(
            "VPN authentication keeps failing",
            "Unable to connect to the corporate VPN from home. The client authenticates " +
                "successfully but shows error 691 during tunnel establishment. The issue started " +
                "over the weekend — VPN was working correctly before.",
            RequestPriority.High, network, employee);
        r1.AssignTo(agent);
        r1.ChangeStatus(RequestStatus.InProgress);
        r1.ChangeStatus(RequestStatus.WaitingForUser);

        // r2 — Resolved, agent assigned (Medium)
        var r2 = new SupportRequest(
            "Laptop overheating during video calls",
            "Dell Latitude 5530 throttles badly after about 20 minutes on a Teams call. " +
                "The fan runs at maximum speed continuously and CPU performance drops significantly. " +
                "Temperatures appear unusually high in Task Manager.",
            RequestPriority.Medium, hardware, employee);
        r2.AssignTo(agent);
        r2.ChangeStatus(RequestStatus.InProgress);
        r2.ChangeStatus(RequestStatus.Resolved);

        // r3 — Closed, agent2 assigned (Low)
        var r3 = new SupportRequest(
            "Microsoft 365 license activation error",
            "Office 365 shows a read-only activation error after my account was migrated to " +
                "the new department tenant. Signing out and back in does not help. I need full " +
                "editing access to complete my work.",
            RequestPriority.Low, software, employee2);
        r3.AssignTo(agent2);
        r3.ChangeStatus(RequestStatus.InProgress);
        r3.ChangeStatus(RequestStatus.Resolved);
        r3.ChangeStatus(RequestStatus.Closed);

        // r4 — New, unassigned (Medium)
        var r4 = new SupportRequest(
            "Access request for Marketing shared drive",
            "I need read access to the Marketing\\Campaigns shared folder for the Q3 project. " +
                "My manager David Chen has approved this. Please grant access at your earliest " +
                "convenience.",
            RequestPriority.Medium, access, employee);

        // r5 — Cancelled, no assignee (High)
        var r5 = new SupportRequest(
            "Wi-Fi dropping in Conference Room B",
            "The wireless network in Conference Room B on the second floor drops every few " +
                "minutes during calls. We had a client presentation disrupted by this yesterday. " +
                "Other areas of the building appear fine.",
            RequestPriority.High, network, employee2);
        r5.ChangeStatus(RequestStatus.Cancelled);

        // r6 — InProgress, agent2 assigned (High)
        var r6 = new SupportRequest(
            "Password reset link not working",
            "The password reset link I received by email shows as expired or invalid. Tried " +
                "three different browsers with the same result. My password expired yesterday " +
                "and I cannot log in to any company systems.",
            RequestPriority.High, access, employee);
        r6.AssignTo(agent2);
        r6.ChangeStatus(RequestStatus.InProgress);

        // r7 — InProgress, agent assigned (Critical)
        var r7 = new SupportRequest(
            "Critical workstation failure — finance team",
            "Workstation WKSTN-FIN-007 will not start and shows three amber LED blinks on " +
                "power-on with no POST. The user has no access to any system. This is critically " +
                "affecting payroll processing for month-end close.",
            RequestPriority.Critical, hardware, employee2);
        r7.AssignTo(agent);
        r7.ChangeStatus(RequestStatus.InProgress);

        // r8 — WaitingForUser, agent assigned (High)
        var r8 = new SupportRequest(
            "Cannot install development tools",
            "I need Node.js 20 LTS, Git for Windows, and VS Code installed for an upcoming " +
                "project. The software center only shows outdated versions. Please install them " +
                "or grant temporary elevated rights.",
            RequestPriority.High, software, employee);
        r8.AssignTo(agent);
        r8.ChangeStatus(RequestStatus.InProgress);
        r8.ChangeStatus(RequestStatus.WaitingForUser);

        // r9 — New, unassigned (Low)
        var r9 = new SupportRequest(
            "Monitor flickering during video calls",
            "My secondary 24-inch LG monitor starts flickering when I join a Teams call but " +
                "works fine in single-monitor mode. Tried two different cables and another port " +
                "on the docking station — same result.",
            RequestPriority.Low, hardware, employee2);

        // r10 — New, unassigned (Medium) — CategoryChanged history added later
        var r10 = new SupportRequest(
            "Teams notifications not appearing on desktop",
            "Microsoft Teams notifications stopped appearing last week. Messages arrive " +
                "inside the app, but there are no toast alerts or taskbar badges. Notification " +
                "settings in both Teams and Windows appear correct.",
            RequestPriority.Medium, other, employee);

        // r11 — Resolved, agent2 assigned (Medium) — PriorityChanged history added later
        var r11 = new SupportRequest(
            "Email signature not updating after company rebrand",
            "The centrally managed Outlook signature still shows the old company logo and " +
                "tagline after the rebrand. Signing out of Outlook and reinstalling the add-in " +
                "have not helped. IT portal confirms the policy was pushed two weeks ago.",
            RequestPriority.Medium, software, employee);
        r11.AssignTo(agent2);
        r11.ChangeStatus(RequestStatus.InProgress);
        r11.ChangeStatus(RequestStatus.Resolved);

        // r12 — Resolved, agent2 assigned (Medium)
        var r12 = new SupportRequest(
            "Network share permissions too restrictive for project team",
            "The Project Atlas shared folder was provisioned with read-only permissions by " +
                "mistake during initial setup. Six team members need read-write access to " +
                "complete their sprint deliverables.",
            RequestPriority.Medium, access, employee2);
        r12.AssignTo(agent2);
        r12.ChangeStatus(RequestStatus.InProgress);
        r12.ChangeStatus(RequestStatus.Resolved);

        // r13 — New, unassigned (Medium) — TitleChanged and DescriptionChanged history added later
        var r13 = new SupportRequest(
            "Cannot install Python 3.12 on workstation",
            "Python 3.12 installation fails with error 0x80070643 after running the installer " +
                "from python.org as administrator. The previous Python 3.10 installation was " +
                "removed cleanly first. Disabling antivirus during install did not help.",
            RequestPriority.Medium, software, employee);

        // r14 — Cancelled, agent assigned (Low)
        var r14 = new SupportRequest(
            "Scanner in document management room not working",
            "The Fujitsu fi-7600 scanner in room 106 jams after the first page or skips " +
                "multiple sheets. The last professional calibration was over 18 months ago. " +
                "A large batch scan job is pending.",
            RequestPriority.Low, hardware, employee2);
        r14.AssignTo(agent);
        r14.ChangeStatus(RequestStatus.InProgress);
        r14.ChangeStatus(RequestStatus.Cancelled);

        return [r0, r1, r2, r3, r4, r5, r6, r7, r8, r9, r10, r11, r12, r13, r14];
    }

    private static List<RequestHistory> CreateHistoryEntries(
        SupportRequest[] r,
        ApplicationUser admin,
        ApplicationUser agent,
        ApplicationUser agent2,
        ApplicationUser employee,
        ApplicationUser employee2,
        RequestCategory software,
        RequestCategory other)
    {
        var h = new List<RequestHistory>();

        // r0: Printer — InProgress, agent
        h.Add(new RequestHistory(r[0], admin, RequestHistoryActions.AssignmentChanged, null, agent.Id.ToString()));
        h.Add(new RequestHistory(r[0], admin, RequestHistoryActions.StatusChanged, "New", "InProgress"));

        // r1: VPN — WaitingForUser, agent
        h.Add(new RequestHistory(r[1], admin, RequestHistoryActions.AssignmentChanged, null, agent.Id.ToString()));
        h.Add(new RequestHistory(r[1], agent, RequestHistoryActions.StatusChanged, "New", "InProgress"));
        h.Add(new RequestHistory(r[1], agent, RequestHistoryActions.StatusChanged, "InProgress", "WaitingForUser"));

        // r2: Laptop — Resolved, agent
        h.Add(new RequestHistory(r[2], admin, RequestHistoryActions.AssignmentChanged, null, agent.Id.ToString()));
        h.Add(new RequestHistory(r[2], agent, RequestHistoryActions.StatusChanged, "New", "InProgress"));
        h.Add(new RequestHistory(r[2], agent, RequestHistoryActions.StatusChanged, "InProgress", "Resolved"));

        // r3: O365 — Closed, agent2
        h.Add(new RequestHistory(r[3], admin, RequestHistoryActions.AssignmentChanged, null, agent2.Id.ToString()));
        h.Add(new RequestHistory(r[3], agent2, RequestHistoryActions.StatusChanged, "New", "InProgress"));
        h.Add(new RequestHistory(r[3], agent2, RequestHistoryActions.StatusChanged, "InProgress", "Resolved"));
        h.Add(new RequestHistory(r[3], employee2, RequestHistoryActions.StatusChanged, "Resolved", "Closed"));

        // r5: Wi-Fi — Cancelled
        h.Add(new RequestHistory(r[5], employee2, RequestHistoryActions.StatusChanged, "New", "Cancelled"));

        // r6: Password — InProgress, agent2
        h.Add(new RequestHistory(r[6], admin, RequestHistoryActions.AssignmentChanged, null, agent2.Id.ToString()));
        h.Add(new RequestHistory(r[6], agent2, RequestHistoryActions.StatusChanged, "New", "InProgress"));

        // r7: Finance workstation — InProgress, agent
        h.Add(new RequestHistory(r[7], admin, RequestHistoryActions.AssignmentChanged, null, agent.Id.ToString()));
        h.Add(new RequestHistory(r[7], admin, RequestHistoryActions.StatusChanged, "New", "InProgress"));

        // r8: Dev tools — WaitingForUser, agent
        h.Add(new RequestHistory(r[8], admin, RequestHistoryActions.AssignmentChanged, null, agent.Id.ToString()));
        h.Add(new RequestHistory(r[8], agent, RequestHistoryActions.StatusChanged, "New", "InProgress"));
        h.Add(new RequestHistory(r[8], agent, RequestHistoryActions.StatusChanged, "InProgress", "WaitingForUser"));

        // r10: Teams notifications — CategoryChanged (was Software, now Other)
        h.Add(new RequestHistory(r[10], admin, RequestHistoryActions.CategoryChanged, software.Id.ToString(), other.Id.ToString()));

        // r11: Email signature — PriorityChanged (Low → Medium), Resolved
        h.Add(new RequestHistory(r[11], admin, RequestHistoryActions.AssignmentChanged, null, agent2.Id.ToString()));
        h.Add(new RequestHistory(r[11], admin, RequestHistoryActions.PriorityChanged, "Low", "Medium"));
        h.Add(new RequestHistory(r[11], agent2, RequestHistoryActions.StatusChanged, "New", "InProgress"));
        h.Add(new RequestHistory(r[11], agent2, RequestHistoryActions.StatusChanged, "InProgress", "Resolved"));

        // r12: Network permissions — Resolved, agent2
        h.Add(new RequestHistory(r[12], admin, RequestHistoryActions.AssignmentChanged, null, agent2.Id.ToString()));
        h.Add(new RequestHistory(r[12], agent2, RequestHistoryActions.StatusChanged, "New", "InProgress"));
        h.Add(new RequestHistory(r[12], agent2, RequestHistoryActions.StatusChanged, "InProgress", "Resolved"));

        // r13: Python install — TitleChanged, DescriptionChanged
        h.Add(new RequestHistory(r[13], employee, RequestHistoryActions.TitleChanged,
            "Python installation request",
            "Cannot install Python 3.12 on workstation"));

        h.Add(new RequestHistory(r[13], employee, RequestHistoryActions.DescriptionChanged,
            "Python 3.12 install fails with error 0x80070643. Running as administrator did not help.",
            "Python 3.12 installation fails with error 0x80070643 after running the installer from python.org as administrator. Th..."));

        // r14: Scanner — Cancelled, agent
        h.Add(new RequestHistory(r[14], admin, RequestHistoryActions.AssignmentChanged, null, agent.Id.ToString()));
        h.Add(new RequestHistory(r[14], agent, RequestHistoryActions.StatusChanged, "New", "InProgress"));
        h.Add(new RequestHistory(r[14], agent, RequestHistoryActions.StatusChanged, "InProgress", "Cancelled"));

        return h;
    }

    private static List<RequestComment> CreateComments(
        SupportRequest[] r,
        ApplicationUser agent,
        ApplicationUser employee)
    {
        var comments = new List<RequestComment>();

        // r0: Printer (InProgress)
        comments.Add(new RequestComment(
            "The printer disconnects within 15 minutes — more frequently in the afternoon. " +
                "Power cycling restores it, but it always comes back.",
            false, r[0], employee));

        comments.Add(new RequestComment(
            "I have reviewed the network logs remotely. This looks like a DHCP lease renewal " +
                "conflict with the printer's static IP. I will reconfigure it to use a reserved " +
                "DHCP address. Could you confirm the MAC address from the label on the back?",
            false, r[0], agent));

        comments.Add(new RequestComment(
            "NIC may be degrading — if DHCP reservation does not stabilise the connection " +
                "within 48 hours, escalate for hardware swap.",
            true, r[0], agent));

        // r1: VPN (WaitingForUser)
        comments.Add(new RequestComment(
            "I have reset your VPN profile and issued updated credentials. Please delete the " +
                "existing VPN connection on your machine, download the updated client from the " +
                "IT portal, and try reconnecting.",
            false, r[1], agent));

        comments.Add(new RequestComment(
            "Thank you — downloaded the new client but still seeing error 691. The username " +
                "field auto-fills with my old email format. Should I change it to the new one?",
            false, r[1], employee));

        comments.Add(new RequestComment(
            "Stale auth tokens in AD from domain migration — coordinating with AD team to flush " +
                "them. Expecting resolution within the next business day.",
            true, r[1], agent));

        // r2: Laptop (Resolved)
        comments.Add(new RequestComment(
            "Replaced the thermal paste and cleaned the cooling vents. Also updated the BIOS " +
                "to version 1.23.0 which includes thermal management improvements for this model. " +
                "Let me know if temperatures are still high.",
            false, r[2], agent));

        comments.Add(new RequestComment(
            "The laptop is running cool now — temperatures look completely normal during calls. " +
                "Really appreciate the quick turnaround!",
            false, r[2], employee));

        // r8: Dev tools (WaitingForUser)
        comments.Add(new RequestComment(
            "Your account is in the standard user group which prevents direct software " +
                "installation. I need written approval from your manager before I can grant " +
                "temporary elevated rights — please ask them to email IT helpdesk to confirm.",
            false, r[8], agent));

        return comments;
    }
}
